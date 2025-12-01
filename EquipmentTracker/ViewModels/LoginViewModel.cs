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
            if (IsBusy) return;
            IsBusy = true;

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

                    if (Application.Current is App app)
                    {
                        app.StartSessionCheck();
                    }

                    // --- YENİ: EĞER ADMİN İSE SENKRONİZASYONU BAŞLAT ---
                    if (user.IsAdmin)
                    {
                        // Arka planda çalıştır, girişi bekletme
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                // JobService'i manuel scope ile alıyoruz
                                // Çünkü LoginViewModel transient olabilir ama işlem uzun sürebilir
                                var jobService = Application.Current.Handler.MauiContext.Services.GetService<IJobService>();

                                // Global yolu kontrol et ve verileri indir
                                await jobService.SyncAllFilesFromFtpAsync();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Auto-Sync Error: {ex.Message}");
                            }
                        });
                    }

                    // ----------------------------------------------------

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
                // KRİTİK HATA (Sunucuya ulaşılamadı vb.)
                if (Application.Current?.MainPage != null)
                    await Application.Current.MainPage.DisplayAlert("Bağlantı Hatası",
                        $"Sunucuya bağlanılamadı. Lütfen internet bağlantınızı ve Sunucu IP ayarlarını kontrol edin.\n\nHata Detayı: {ex.Message}",
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

        public async Task<bool> CheckInternetAndDbConnectionLoop()
        {
            while (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                // İnternet yoksa döngüye gir
                bool retry = await Application.Current.MainPage.DisplayAlert(
                    "Bağlantı Hatası",
                    "İnternet bağlantısı bulunamadı.\nLütfen internetinizi açın ve 'Tekrar Dene' butonuna basın.",
                    "Tekrar Dene",
                    "Uygulamayı Kapat");

                if (!retry)
                {
                    // Kullanıcı kapat dedi
                    Application.Current.Quit();
                    return false;
                }

                // Kullanıcı "Tekrar Dene" dediğinde döngü başa döner ve tekrar kontrol eder.
            }

            // İnternet var, şimdi DB başlatmayı dene
            if (!MauiProgram.IsDatabaseInitialized)
            {
                try
                {
                    await _jobService.InitializeDatabaseAsync();
                    MauiProgram.IsDatabaseInitialized = true;
                }
                catch (Exception ex)
                {
                    // DB Hatası varsa yine uyar ve gerekirse döngüye sokabilirsin
                    await Application.Current.MainPage.DisplayAlert("Sunucu Hatası", $"Sunucuya erişilemedi: {ex.Message}\nAyarları kontrol edin.", "Tamam");
                    return false;
                }
            }

            return true;
        }

    }
}