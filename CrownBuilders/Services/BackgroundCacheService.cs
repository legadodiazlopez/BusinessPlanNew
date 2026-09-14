using System;
using System.Threading;
using System.Threading.Tasks;

namespace CrownBuilders.Services
{
    /// <summary>
    /// Servicio de caché en segundo plano para precarga asíncrona de datos
    /// Ejecuta operaciones de sincronización de forma transparente sin bloquear la UI
    /// </summary>
    public static class BackgroundCacheService
    {
        private static readonly object _lock = new object();
        private static bool _isInitialized = false;
        private static CancellationTokenSource? _cancellationTokenSource;

        /// <summary>
        /// Inicia la precarga en segundo plano de compañías y trabajadores
        /// Se ejecuta de forma "fire-and-forget" sin bloquear la UI
        /// </summary>
        public static void StartBackgroundPreload()
        {
            lock (_lock)
            {
                if (_isInitialized)
                {
                    System.Diagnostics.Debug.WriteLine("[BACKGROUND CACHE] Ya está inicializado, omitiendo");
                    return;
                }

                _isInitialized = true;
                _cancellationTokenSource = new CancellationTokenSource();

                // Ejecutar en segundo plano de forma totalmente aislada
                _ = Task.Run(async () => await PreloadDataSafelyAsync(_cancellationTokenSource.Token));

                System.Diagnostics.Debug.WriteLine("[BACKGROUND CACHE] Precarga en segundo plano iniciada");
            }
        }

        /// <summary>
        /// Precarga asíncrona segura con control de tiempos para evitar saturar sockets de Azure
        /// </summary>
        private static async Task PreloadDataSafelyAsync(CancellationToken cancellationToken)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[BACKGROUND CACHE] Iniciando precarga diferida de datos...");

                // Dar un respiro a la UI y al proceso de login antes de disparar peticiones de red
                await Task.Delay(1500, cancellationToken);

                int trabajadoresCount = 0;
                int companiasCount = 0;

                // 1. Precargar trabajadores de forma segura
                try
                {
                    var trabajadores = await DatabaseHelper.ObtenerTrabajadoresAsync();
                    trabajadoresCount = trabajadores?.Count ?? 0;
                }
                catch (Exception exTr)
                {
                    System.Diagnostics.Debug.WriteLine($"[BACKGROUND CACHE] Aviso al precargar trabajadores: {exTr.Message}");
                }

                // Pequeño respiro entre peticiones para no saturar el socket HTTP de Azure
                await Task.Delay(500, cancellationToken);

                // 2. Precargar compañías de forma segura
                try
                {
                    var companias = await DatabaseHelper.ObtenerCompaniasAsync();
                    companiasCount = companias?.Count ?? 0;
                }
                catch (Exception exCo)
                {
                    System.Diagnostics.Debug.WriteLine($"[BACKGROUND CACHE] Aviso al precargar compañías: {exCo.Message}");
                }

                System.Diagnostics.Debug.WriteLine($"[BACKGROUND CACHE] Precarga completada sin fricción: {trabajadoresCount} trabajadores, {companiasCount} compañías");
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[BACKGROUND CACHE] Precarga cancelada");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BACKGROUND CACHE] Error general en precarga: {ex.Message}");
            }
        }

        /// <summary>
        /// Detiene la precarga en segundo plano
        /// </summary>
        public static void StopBackgroundPreload()
        {
            lock (_lock)
            {
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }
                _isInitialized = false;
                System.Diagnostics.Debug.WriteLine("[BACKGROUND CACHE] Precarga detenida");
            }
        }

        /// <summary>
        /// Reinicia el servicio de caché en segundo plano
        /// </summary>
        public static void RestartBackgroundPreload()
        {
            StopBackgroundPreload();
            StartBackgroundPreload();
        }
    }
}