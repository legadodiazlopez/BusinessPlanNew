using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;

namespace CrownBuilders.Diagnostics
{
    /// <summary>
    /// Watchdog para apps .NET MAUI Blazor Hybrid.
    /// A diferencia del watchdog original (que vigilaba un proceso EXTERNO),
    /// este vive DENTRO de tu propia app MAUI (porque MAUI Blazor Hybrid corre
    /// en un solo proceso) y detecta:
    ///   1) Excepciones no controladas (UI thread y background tasks)
    ///   2) Congelamientos del hilo principal (UI freeze)
    ///   3) Consumo de memoria excesivo
    ///
    /// Cuando detecta una anomalía:
    ///   - Captura screenshot de la app (Microsoft.Maui.Media.Screenshot)
    ///   - Junta los últimos logs (buffer propio, no depende de un archivo externo)
    ///   - Intenta extraer el código fuente del archivo/línea que falló
    ///   - Envía el paquete a GPT-4o para diagnóstico (opcional)
    ///   - Notifica DIRECTO a Visual Studio: aparece en la ventana "Output" con
    ///     un bloque bien marcado, y si tienes el debugger pegado, pausa la
    ///     ejecución automáticamente justo en el punto del fallo (Debugger.Break()).
    ///
    /// USO — en MauiProgram.cs, dentro de CreateMauiApp(), antes de "return builder.Build();":
    ///
    ///     WatchdogAgent.Initialize(new WatchdogOptions
    ///     {
    ///         OpenAiApiKey   = "TU_API_KEY_DE_OPENAI", // opcional, deja "" para desactivar IA
    ///         ProjectRootPath = @"C:\Ruta\A\Tu\Proyecto",
    ///         MemoryThresholdMb = 2048,
    ///         FreezeThresholdMs = 3000
    ///     });
    /// </summary>
    public static class WatchdogAgent
    {
        private static WatchdogOptions _options = new();
        private static readonly ConcurrentQueue<string> _logBuffer = new();
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(60) };
        private static Timer? _freezeTimer;
        private static DateTime _lastHeartbeat = DateTime.UtcNow;
        private static bool _diagnosticInProgress = false;
        private const int MaxLogLines = 200;

        public static void Initialize(WatchdogOptions options)
        {
            _options = options ?? new WatchdogOptions();

            // 1) Capturamos todo lo que se escriba con Debug/Trace en un buffer propio,
            //    así no dependemos de que exista un archivo app.log en disco.
            Trace.Listeners.Add(new RingBufferTraceListener(_logBuffer, MaxLogLines));

            // 2) Excepciones no controladas en cualquier hilo
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                _ = TriggerDiagnosticsAsync(ex, "Excepción no controlada (AppDomain).", isFatal: e.IsTerminating);
            };

            // 3) Excepciones en Tasks que nadie hizo "await"
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                _ = TriggerDiagnosticsAsync(e.Exception, "UnobservedTaskException (Task en background sin await).", isFatal: false);
                e.SetObserved();
            };

            // 4) Heartbeat: cada X segundos pedimos al hilo de UI que "responda".
            //    Si no responde a tiempo, asumimos freeze.
            _freezeTimer = new Timer(_ => CheckFreeze(), null, 1000, 1000);

            LogInternal("[Watchdog] Inicializado correctamente.");
        }

        /// <summary>Llama a esto UNA vez por frame/ciclo desde tu app (p.ej. en un timer de UI
        /// cada 500ms, o en OnAppearing de tu MainPage) para reportar que el hilo principal está vivo.</summary>
        public static void Heartbeat() => _lastHeartbeat = DateTime.UtcNow;

        private static void CheckFreeze()
        {
            try
            {
                var elapsed = (DateTime.UtcNow - _lastHeartbeat).TotalMilliseconds;
                var proc = Process.GetCurrentProcess();
                long workingSetMb = proc.WorkingSet64 / (1024 * 1024);

                if (elapsed > _options.FreezeThresholdMs)
                {
                    _ = TriggerDiagnosticsAsync(null, $"UI congelada: sin heartbeat por {elapsed:N0} ms.", isFatal: false);
                }
                else if (workingSetMb > _options.MemoryThresholdMb)
                {
                    _ = TriggerDiagnosticsAsync(null, $"Consumo de memoria excesivo: {workingSetMb} MB.", isFatal: false);
                }
            }
            catch (Exception ex)
            {
                LogInternal($"[Watchdog] Error en CheckFreeze: {ex.Message}");
            }
        }

        public static async Task TriggerDiagnosticsAsync(Exception? ex, string reason, bool isFatal)
        {
            if (_diagnosticInProgress) return; // evita reentradas si ya estamos procesando un fallo
            _diagnosticInProgress = true;

            try
            {
                LogInternal($"[Watchdog] Anomalía detectada: {reason}");

                // --- 1. Screenshot (best-effort, no todas las plataformas lo soportan igual) ---
                string base64Image = string.Empty;
                try
                {
                    if (Screenshot.Default.IsCaptureSupported)
                    {
                        var result = await Screenshot.Default.CaptureAsync();
                        using var stream = await result.OpenReadAsync();
                        using var ms = new MemoryStream();
                        await stream.CopyToAsync(ms);
                        base64Image = Convert.ToBase64String(ms.ToArray());
                    }
                }
                catch (Exception exShot)
                {
                    LogInternal($"[Watchdog] No se pudo capturar screenshot: {exShot.Message}");
                }

                // --- 2. Logs recientes (buffer interno) ---
                string logs = string.Join("\n", _logBuffer);
                if (string.IsNullOrWhiteSpace(logs)) logs = "Sin logs recientes.";

                // --- 3. Stack trace + código fuente relacionado ---
                string stackTrace = ex?.ToString() ?? "No hay excepción .NET asociada (posible freeze o memoria).";
                string sourceCode = TryExtractSourceCode(stackTrace);

                // --- 4. Métricas ---
                var proc = Process.GetCurrentProcess();
                string metrics = $"Razón: {reason} | RAM: {proc.WorkingSet64 / (1024 * 1024)} MB | Fatal: {isFatal}";

                // --- 5. Notificación DIRECTA a Visual Studio ---
                NotifyVisualStudio(reason, stackTrace, metrics);

                // --- 6. Guardar reporte local (respaldo si no hay internet / API key) ---
                SaveLocalCrashReport(reason, stackTrace, logs, metrics);

                // --- 7. Diagnóstico con IA (opcional) ---
                if (!string.IsNullOrWhiteSpace(_options.OpenAiApiKey))
                {
                    await SendToOpenAiAsync(base64Image, logs, sourceCode, metrics, stackTrace);
                }
            }
            catch (Exception exDiag)
            {
                LogInternal($"[Watchdog Fallo en Diagnóstico]: {exDiag.Message}");
            }
            finally
            {
                _diagnosticInProgress = false;
            }
        }

        /// <summary>
        /// Notifica directamente a Visual Studio. Esto es lo más directo y confiable
        /// que se puede hacer sin infraestructura extra:
        ///  - Debug.WriteLine imprime en la ventana "Output" de VS mientras depuras.
        ///  - Debugger.Break() pausa la ejecución en VS justo en este punto (solo si
        ///    hay un debugger pegado), como si hubieras puesto un breakpoint aquí.
        /// </summary>
        private static void NotifyVisualStudio(string reason, string stackTrace, string metrics)
        {
            Debug.WriteLine("==================================================");
            Debug.WriteLine("=== WATCHDOG: ANOMALÍA DETECTADA EN LA APP MAUI ===");
            Debug.WriteLine("==================================================");
            Debug.WriteLine($"Motivo : {reason}");
            Debug.WriteLine($"Métricas: {metrics}");
            Debug.WriteLine("--- Stack trace ---");
            Debug.WriteLine(stackTrace);
            Debug.WriteLine("==================================================");

            if (Debugger.IsAttached)
            {
                // Pausa la ejecución en Visual Studio, en este punto exacto.
                Debugger.Break();
            }
        }

        private static void SaveLocalCrashReport(string reason, string stackTrace, string logs, string metrics)
        {
            try
            {
                string folder = Path.Combine(FileSystem.AppDataDirectory, "watchdog_reports");
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(file,
                    $"REPORTE DE WATCHDOG\nFecha: {DateTime.Now}\nMotivo: {reason}\nMétricas: {metrics}\n\n" +
                    $"--- STACK TRACE ---\n{stackTrace}\n\n--- LOGS ---\n{logs}");
            }
            catch (Exception ex)
            {
                LogInternal($"[Watchdog] No se pudo guardar el reporte local: {ex.Message}");
            }
        }

        private static string TryExtractSourceCode(string stackTrace)
        {
            try
            {
                // Busca rutas tipo "in C:\...\Archivo.cs:line 45" o "in .../Archivo.razor:line 12"
                Match match = Regex.Match(stackTrace, @"in\s+(.:\\.+?\.(cs|razor)):line\s+(\d+)");
                if (match.Success)
                {
                    string filePath = match.Groups[1].Value;
                    if (File.Exists(filePath))
                    {
                        string code = File.ReadAllText(filePath);
                        return code.Length > 4000 ? code[..4000] : code;
                    }
                }
            }
            catch { /* best-effort */ }
            return "No se pudo extraer código fuente (build Release o ruta no disponible).";
        }

        private static async Task SendToOpenAiAsync(string base64Image, string logs, string code, string metrics, string stackTrace)
        {
            try
            {
                LogInternal("[Watchdog] Enviando paquete de diagnóstico a OpenAI...");

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _options.OpenAiApiKey);

                string prompt = $"""
                Actúa como Arquitecto de Software Senior experto en .NET MAUI Blazor Hybrid.
                Analiza la anomalía detectada en la app en ejecución:

                MÉTRICAS:
                {metrics}

                STACK TRACE:
                {stackTrace}

                LOGS RECIENTES:
                {logs}

                CÓDIGO FUENTE RELEVANTE:
                {code}

                Responde en 3 secciones exactas:
                1. CAUSA RAÍZ
                2. UBICACIÓN EXACTA (archivo y línea)
                3. CÓDIGO CORREGIDO
                """;

                var contentBlocks = new System.Collections.Generic.List<object>
                {
                    new { type = "text", text = prompt }
                };
                if (!string.IsNullOrEmpty(base64Image))
                {
                    contentBlocks.Add(new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{base64Image}" } });
                }

                var payload = new
                {
                    model = "gpt-4o",
                    messages = new[] { new { role = "user", content = contentBlocks } },
                    max_tokens = 1500
                };

                string json = JsonSerializer.Serialize(payload);
                using var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", httpContent);
                string responseString = await response.Content.ReadAsStringAsync();

                Debug.WriteLine("=== DIAGNÓSTICO IA (GPT-4o) ===");
                Debug.WriteLine(responseString);
                Debug.WriteLine("================================");
            }
            catch (Exception ex)
            {
                LogInternal($"[Watchdog] Fallo al llamar a OpenAI: {ex.Message}");
            }
        }

        private static void LogInternal(string message)
        {
            Debug.WriteLine(message);
            _logBuffer.Enqueue($"{DateTime.Now:HH:mm:ss} {message}");
            while (_logBuffer.Count > MaxLogLines) _logBuffer.TryDequeue(out _);
        }

        /// <summary>TraceListener que guarda todo lo que la app escribe con Debug/Trace
        /// en un buffer circular, para poder enviarlo como "logs" al detectar un fallo.</summary>
        private class RingBufferTraceListener : TraceListener
        {
            private readonly ConcurrentQueue<string> _buffer;
            private readonly int _max;

            public RingBufferTraceListener(ConcurrentQueue<string> buffer, int max)
            {
                _buffer = buffer;
                _max = max;
            }

            public override void Write(string? message) => Add(message);
            public override void WriteLine(string? message) => Add(message);

            private void Add(string? message)
            {
                if (string.IsNullOrEmpty(message)) return;
                _buffer.Enqueue($"{DateTime.Now:HH:mm:ss} {message}");
                while (_buffer.Count > _max) _buffer.TryDequeue(out _);
            }
        }
    }
}
