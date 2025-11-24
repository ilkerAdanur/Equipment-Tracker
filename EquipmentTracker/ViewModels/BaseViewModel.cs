using CommunityToolkit.Mvvm.ComponentModel;
using EquipmentTracker.Services.Auth;
using EquipmentTracker.Views;
using Microsoft.Extensions.DependencyInjection; // Gerekli

namespace EquipmentTracker.ViewModels
{
    public partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool _isBusy;

        [ObservableProperty]
        private string _title;

        public bool IsNotBusy => !IsBusy;

        // --- YENİ GÜVENLİK KONTROLÜ ---
        protected async Task<bool> CheckSessionAsync()
        {
            if (App.CurrentUser == null) return false;

            try
            {
                // Servisi al (BaseViewModel'da DI olmadığı için ServiceProvider üzerinden alıyoruz)
                var authService = Application.Current.Handler.MauiContext.Services.GetService<IAuthService>();

                bool isActive = await authService.IsUserActiveAsync(App.CurrentUser.Id);

                if (!isActive)
                {
                    await Shell.Current.DisplayAlert("Oturum Sonlandı",
                        "Bağlantınız yönetici tarafından kesildi veya başka bir yerden giriş yapıldı.", "Çıkış");

                    // Login ekranına at
                    App.CurrentUser = null;
                    var loginPage = Application.Current.Handler.MauiContext.Services.GetService<LoginPage>();
                    Application.Current.MainPage = new NavigationPage(loginPage);
                    return false;
                }
                return true;
            }
            catch
            {
                // Veritabanı hatası vs. olursa güvenli tarafta kalıp devam ettirebilir veya engelleyebiliriz.
                // Şimdilik devam etsin ama loglanabilir.
                return true;
            }
        }
    }
}