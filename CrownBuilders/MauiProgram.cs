using Microsoft.Extensions.Logging;
using CrownBuilders.Services;

namespace CrownBuilders
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            // Registramos tus servicios de negocio
            builder.Services.AddSingleton<SyncService>();
            builder.Services.AddSingleton<StripeService>();

            return builder.Build();
        }
    }
}