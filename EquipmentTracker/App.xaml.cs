// Dosya: App.xaml.cs
namespace EquipmentTracker
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            MainPage = new AppShell();
        }

        // YENİ EKLENEN METOT (Pencere Boyutu için)
        protected override Window CreateWindow(IActivationState activationState)
        {
            var window = base.CreateWindow(activationState);

            // İstediğiniz varsayılan boyutu ayarlayın
            const int WindowWidth = 1280;
            const int WindowHeight = 800;

            window.Width = WindowWidth;
            window.Height = WindowHeight;

            // (Bu satırlar pencereyi ortalar - opsiyonel)
            window.X = -1;
            window.Y = -1;

            return window;
        }
    }
}