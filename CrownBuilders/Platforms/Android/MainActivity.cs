using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using CrownBuilders.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;

namespace CrownBuilders
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "crownbuilders",
        DataHost = "checkout")]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            HandleCheckoutIntent(Intent);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            HandleCheckoutIntent(intent);
        }

        private static void HandleCheckoutIntent(Intent? intent)
        {
            var uri = intent?.DataString;
            if (string.IsNullOrWhiteSpace(uri))
            {
                return;
            }

            IPlatformApplication.Current?.Services
                .GetService<CheckoutReturnService>()
                ?.Handle(uri);
        }
    }
}
