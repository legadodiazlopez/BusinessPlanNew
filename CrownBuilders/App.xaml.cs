using CrownBuilders.Services;

namespace CrownBuilders
{
    public partial class App : Application
    {
        public App(CheckoutReturnService checkoutReturnService)
        {
            InitializeComponent();

            MainPage = new MainPage(checkoutReturnService);
        }
    }
}
