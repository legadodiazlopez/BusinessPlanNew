namespace CrownBuilders
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Forzamos a que la página principal inicialice el contenedor híbrido de Blazor
            MainPage = new MainPage();
        }
    }
}