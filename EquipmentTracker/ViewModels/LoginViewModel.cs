using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentTracker.Services.Auth;
using EquipmentTracker.Services.Job;
using EquipmentTracker.Views; // InitializeDatabase için

namespace EquipmentTracker.ViewModels
{
    public partial class LoginViewModel : BaseViewModel
    {
        private readonly IAuthService _authService;
        private readonly IJobService _jobService; // DB'yi oluşturmak için buna ihtiyacımız var

        [ObservableProperty]
        string _username;

        [ObservableProperty]
        string _password;

        public LoginViewModel(IAuthService authService, IJobService jobService)
        {
            _authService = authService;
            _jobService = jobService;
        }

        [RelayCommand]
        async Task Login()
        {
            if (IsBusy) return; // Zaten işlem yapılıyorsa tekrar basmayı engelle
            IsBusy = true;      // Çark dönmeye başlasın

            try
            {
                // 1. Veritabanı Oluşturma Kontrolü (Sadece bir kez)
                if (!MauiProgram.IsDatabaseInitialized)
                {
                    // Burada hata alırsak catch'e düşer ve mesaj gösteririz
                    await _jobService.InitializeDatabaseAsync();
                    MauiProgram.IsDatabaseInitialized = true;
                }

                // 2. Basit Kontroller
                if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
                {
                    if (Application.Current?.MainPage != null)
                        await Application.Current.MainPage.DisplayAlert("Uyarı", "Kullanıcı adı ve şifre gereklidir.", "Tamam");
                    return;
                }

                // 3. Giriş İşlemi
                var user = await _authService.LoginAsync(Username, Password);

                if (user != null)
                {
                    // BAŞARILI
                    App.CurrentUser = user;
                    Application.Current.MainPage = new AppShell();
                }
                else
                {
                    // BAŞARISIZ (Kullanıcı yok veya şifre yanlış)
                    if (Application.Current?.MainPage != null)
                        await Application.Current.MainPage.DisplayAlert("Hata", "Kullanıcı adı veya şifre hatalı.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                // SqlException yerine Exception kullanımı daha genel ve güvenlidir
                if (Application.Current?.MainPage != null)
                    await Application.Current.MainPage.DisplayAlert("Bağlantı Hatası",
                        $"Sunucuya bağlanılamadı.\nHata: {ex.Message}",
                        "Tamam");
            }
            finally
            {
                // *** BU KISIM HAYAT KURTARIR ***
                // Hata olsa da, giriş yapılsa da, şifre yanlış olsa da BURASI ÇALIŞIR.
                // Çarkı durdurur ve donmayı önler.
                IsBusy = false;
            }
        }
        // Ayarlar sayfasına gitmek için (IP değiştirmek gerekebilir)
        [RelayCommand]
        async Task GoToSettings()
        {
            // Settings sayfasını aç
            var services = Application.Current.Handler.MauiContext.Services;
            await Application.Current.MainPage.Navigation.PushAsync(new SettingsPage(
                new SettingsViewModel(CommunityToolkit.Maui.Storage.FolderPicker.Default, services)));
        }
    }
}