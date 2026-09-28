using Microsoft.Extensions.Logging;
using CrownBuilders.Services;
using CrownBuilders.Diagnostics;
using Microsoft.AspNetCore.SignalR.Client;
using System.Reflection;
using System.Net.Http;

namespace CrownBuilders
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            try
            {
                var builder = MauiApp.CreateBuilder();

                builder
                    .UseMauiApp<App>()
                    .ConfigureFonts(fonts =>
                    {
                        try
                        {
                            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                        }
                        catch (System.Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error configurando fuentes: {ex.Message}");
                        }
                    });

                builder.Services.AddMauiBlazorWebView();

#if DEBUG
                try
                {
                    builder.Services.AddBlazorWebViewDeveloperTools();
                    builder.Logging.AddDebug();
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error configurando herramientas de desarrollo: {ex.Message}");
                }
#endif

                // Registramos servicios con manejo de errores
                try
                {
                    builder.Services.AddSingleton<ApiService>();
                    builder.Services.AddSingleton<CheckoutReturnService>();
                    builder.Services.AddSingleton<MessagingService>();
                    builder.Services.AddSingleton<SyncService>();
                    builder.Services.AddSingleton(new HttpClient
                    {
                        // Producción/Azure: https://crownbuilders-portal-drbqbfbphpe7hude.centralus-01.azurewebsites.net/api/
                        // Desarrollo local:
                        BaseAddress = new Uri(ApiEndpoints.ApiBaseUrl),
                        Timeout = TimeSpan.FromSeconds(30)
                    });
                    builder.Services.AddSingleton<StripeService>();
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error registrando servicios: {ex.Message}");
                }

                var app = builder.Build();

                // INICIALIZAR WATCHDOG DE DIAGNÓSTICO
                try
                {
                    WatchdogAgent.Initialize(new WatchdogOptions
                    {
                        OpenAiApiKey = "", // Configurar con API key de OpenAI si se desea diagnóstico IA
                        ProjectRootPath = @"C:\Users\david\source\repos\CrownBuilders",
                        MemoryThresholdMb = 2048,
                        FreezeThresholdMs = 3000
                    });
                    System.Diagnostics.Debug.WriteLine("[WATCHDOG] Agente de diagnóstico inicializado");
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WATCHDOG] Error inicializando watchdog: {ex.Message}");
                }

                // PRECALENTAMIENTO DE SIGNALR Y WEBSOCKETS PARA EVITAR CARGA PEREZOSA
                Task.Run(() =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("[PRE-WARMING] Iniciando precalentamiento de SignalR y WebSockets...");

                        // Forzar carga de asambleas de SignalR
                        var signalRAssembly = Assembly.Load("Microsoft.AspNetCore.SignalR.Client");
                        var webSocketsAssembly = Assembly.Load("System.Net.WebSockets");
                        var httpAssembly = Assembly.Load("System.Net.Http");
                        var extensionsAssembly = Assembly.Load("Microsoft.Extensions.Features");

                        System.Diagnostics.Debug.WriteLine($"[PRE-WARMING] SignalR assembly cargado: {signalRAssembly?.FullName}");
                        System.Diagnostics.Debug.WriteLine($"[PRE-WARMING] WebSockets assembly cargado: {webSocketsAssembly?.FullName}");
                        System.Diagnostics.Debug.WriteLine($"[PRE-WARMING] HTTP assembly cargado: {httpAssembly?.FullName}");
                        System.Diagnostics.Debug.WriteLine($"[PRE-WARMING] Extensions assembly cargado: {extensionsAssembly?.FullName}");

                        // Forzar inicialización de tipos críticos de SignalR
                        var hubConnectionBuilderType = typeof(HubConnectionBuilder);
                        var hubConnectionType = typeof(HubConnection);
                        var httpTransportTypeType = typeof(Microsoft.AspNetCore.Http.Connections.HttpTransportType);

                        System.Diagnostics.Debug.WriteLine($"[PRE-WARMING] Tipos SignalR inicializados: HubConnectionBuilder, HubConnection, HttpTransportType");

                        // Simular configuración básica para precalentar internal state
                        var builder = new HubConnectionBuilder();
                        System.Diagnostics.Debug.WriteLine("[PRE-WARMING] HubConnectionBuilder instanciado correctamente");

                        System.Diagnostics.Debug.WriteLine("[PRE-WARMING] ✅ Precalentamiento completado exitosamente");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PRE-WARMING] ⚠️ Error durante precalentamiento (no crítico): {ex.Message}");
                    }
                });

                return app;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error crítico en CreateMauiApp: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}
