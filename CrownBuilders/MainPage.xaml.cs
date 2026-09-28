using CrownBuilders.Diagnostics;
using CrownBuilders.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;

namespace CrownBuilders
{
    public partial class MainPage : ContentPage
    {
        private readonly CheckoutReturnService _checkoutReturnService;
        private Timer? _heartbeatTimer;

        public MainPage(CheckoutReturnService checkoutReturnService)
        {
            InitializeComponent();
            _checkoutReturnService = checkoutReturnService;
            _checkoutReturnService.ReturnReceived += OnCheckoutReturnReceived;

            // Iniciar heartbeat del watchdog para detectar congelamientos de UI
            StartWatchdogHeartbeat();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _ = NavigateToPendingCheckoutReturnAsync();
        }

        private void OnCheckoutReturnReceived(object? sender, CheckoutReturn checkoutReturn)
        {
            _ = NavigateToPendingCheckoutReturnAsync();
        }

        private async Task NavigateToPendingCheckoutReturnAsync()
        {
            var checkoutReturn = _checkoutReturnService.GetPending();
            if (checkoutReturn is null)
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var dispatched = await blazorWebView.TryDispatchAsync(services =>
                {
                    var navigation = services.GetRequiredService<NavigationManager>();
                    navigation.NavigateTo($"/portal-compania?checkout={checkoutReturn.Result}");
                });

                if (dispatched)
                {
                    _checkoutReturnService.ClearPending();
                }
            });
        }

        private void StartWatchdogHeartbeat()
        {
            _heartbeatTimer = new Timer(_ =>
            {
                WatchdogAgent.Heartbeat();
            }, null, 0, 500);
        }

        protected override void OnDisappearing()
        {
            _heartbeatTimer?.Dispose();
            base.OnDisappearing();
        }
    }
}
