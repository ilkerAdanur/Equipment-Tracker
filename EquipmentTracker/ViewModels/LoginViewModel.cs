using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentTracker.Services.Auth;
using EquipmentTracker.Services.Job; // InitializeDatabase için

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
            Title = "Giriş Yap";
        }

        [RelayCommand]
        async Task Login()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // 1. Veritabanının var olduğundan emin ol (İlk açılışta çok önemli)
                if (!MauiProgram.IsDatabaseInitialized)
                {
                    await _jobService.InitializeDatabaseAsync();
                    MauiProgram.IsDatabaseInitialized = true;
                }

                // 2. Kullanıcı adı şifre kontrolü
                if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
                {
                    await Shell.Current.DisplayAlert("Hata", "Lütfen kullanıcı adı ve şifre girin.", "Tamam");
                    return;
                }

                var user = await _authService.LoginAsync(Username, Password);

                if (user != null)
                {
                    // 3. BAŞARILI: Kullanıcıyı global değişkene ata
                    App.CurrentUser = user;

                    // 4. Ana Uygulamaya (AppShell) Geçiş Yap
                    // Application.Current.MainPage'i değiştirerek navigasyon yığınını sıfırlıyoruz.
                    Application.Current.MainPage = new AppShell();
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "Kullanıcı adı veya şifre hatalı.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", $"Giriş yapılırken hata oluştu. Veritabanı bağlantısını kontrol edin.\n{ex.Message}", "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Ayarlar sayfasına gitmek için (IP değiştirmek gerekebilir)
        [RelayCommand]
        async Task GoToSettings()
        {
            await Application.Current.MainPage.Navigation.PushAsync(new Views.SettingsPage(new SettingsViewModel(CommunityToolkit.Maui.Storage.FolderPicker.Default)));
        }
    }
}