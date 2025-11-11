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
            const int WindowWidth = 525;
            const int WindowHeight = 570;
            // (Bu satırlar pencereyi ortalar - opsiyonel)

            window.MinimumWidth = WindowWidth;
            window.MinimumHeight = WindowHeight;

            window.X = 400;
            window.Y = 20;
            // Başlangıç boyutunu da ayarlayabiliriz
            window.Width = WindowWidth;
            window.Height = WindowHeight;

            return window;
        }
    }
}