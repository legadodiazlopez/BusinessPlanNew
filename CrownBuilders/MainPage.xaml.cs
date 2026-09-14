using CrownBuilders.Diagnostics;
using System.Threading;

namespace CrownBuilders
{
    public partial class MainPage : ContentPage
    {
        private Timer? _heartbeatTimer;

        public MainPage()
        {
            InitializeComponent();

            // Iniciar heartbeat del watchdog para detectar congelamientos de UI
            StartWatchdogHeartbeat();
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
