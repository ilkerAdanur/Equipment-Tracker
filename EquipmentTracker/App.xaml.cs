// Dosya: App.xaml.cs
using EquipmentTracker.Models;
using EquipmentTracker.Views;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentTracker
{
    public partial class App : Application
    {
        public static User? CurrentUser { get; set; }
        public App(IServiceProvider serviceProvider)
        {
            InitializeComponent();

            MainPage = new NavigationPage(serviceProvider.GetRequiredService<LoginPage>());
        }

        // YENİ EKLENEN METOT (Pencere Boyutu için)
        protected override Window CreateWindow(IActivationState activationState)
        {
            var window = base.CreateWindow(activationState);

            // İstediğiniz varsayılan boyutu ayarlayın
            const int WindowWidth = 525;
            const int WindowHeight = 650;
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