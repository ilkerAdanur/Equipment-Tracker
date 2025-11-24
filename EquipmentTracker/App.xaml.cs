using EquipmentTracker.Models;
using EquipmentTracker.Services.Auth;
using EquipmentTracker.Views;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentTracker
{
    public partial class App : Application
    {
        public static User? CurrentUser { get; set; }
        private readonly IServiceProvider _serviceProvider;

        public App(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;

            MainPage = new NavigationPage(serviceProvider.GetRequiredService<LoginPage>());
        }

        protected override Window CreateWindow(IActivationState activationState)
        {
            var window = base.CreateWindow(activationState);

            const int WindowWidth = 525;
            const int WindowHeight = 650;

            window.MinimumWidth = WindowWidth;
            window.MinimumHeight = WindowHeight;
            window.Width = WindowWidth;
            window.Height = WindowHeight;
            window.X = 400;
            window.Y = 20;

            // --- YENİ: PENCERE KAPANDIĞINDA ÇALIŞACAK OLAY ---
            window.Destroying += Window_Destroying;

            return window;
        }

        private void Window_Destroying(object sender, EventArgs e)
        {
            // Uygulama kapanıyor, kullanıcıyı Offline yap
            if (CurrentUser != null)
            {
                // Senkron (bloklayıcı) çalıştırmamız lazım çünkü uygulama kapanmak üzere
                var authService = _serviceProvider.GetService<IAuthService>();

                // Task.Run ile arka planda çalıştırıp sonucunu beklemiyoruz (Fire and forget)
                // Ancak veritabanı bağlantısı kapanmadan yetişmesi için Task.Wait kullanılabilir
                // En temiz yöntem:
                Task.Run(async () => await authService.LogoutAsync(CurrentUser.Id)).Wait();
            }
        }
    }
}