using EquipmentTracker.Models;
using EquipmentTracker.Services.Auth;
using EquipmentTracker.Services.Job;
using EquipmentTracker.Views;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentTracker
{
    public partial class App : Application
    {
        public static Users? CurrentUser { get; set; }
        private readonly IServiceProvider _serviceProvider;

        // Arka plan kontrolünü durdurmak için token
        private CancellationTokenSource _cancellationTokenSource;

        public App(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;

            MainPage = new NavigationPage(serviceProvider.GetRequiredService<LoginPage>());

            // İnternet durumunu dinle
            Connectivity.Current.ConnectivityChanged += Current_ConnectivityChanged;
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

            window.Destroying += Window_Destroying;

            return window;
        }

        // Uygulama Başlatıldığında
        protected override void OnStart()
        {
            base.OnStart();
            StartSessionCheck();
        }

        // Uygulama Arka Plandan Geri Döndüğünde
        protected override void OnResume()
        {
            base.OnResume();
            StartSessionCheck();
        }

        // Uygulama Arka Plana Atıldığında (Pil tasarrufu için durdur)
        protected override void OnSleep()
        {
            base.OnSleep();
            _cancellationTokenSource?.Cancel();
        }

        // PENCERE KAPATILIRKEN (X tuşu)
        private void Window_Destroying(object sender, EventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            if (CurrentUser != null && Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                // Sadece internet varsa DB'den düşürmeye çalış
                try
                {
                    var authService = _serviceProvider.GetService<IAuthService>();
                    Task.Run(async () => await authService.LogoutAsync(CurrentUser.Id)).Wait();
                }
                catch { }
            }
        }

        // --- YENİ: OTURUM KONTROL MEKANİZMASI ---
        private void StartSessionCheck()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(5000, token);

                        // İnternet yoksa döngüyü pas geç (Çökmemesi için)
                        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                        {
                            continue;
                        }

                        if (CurrentUser != null)
                        {
                            using (var scope = _serviceProvider.CreateScope())
                            {
                                var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

                                // BURADA HATA OLABİLİR (SQL Timeout vs.) TRY-CATCH İÇİNDE KALMALI
                                bool isActive = await authService.IsUserActiveAsync(CurrentUser.Id);

                                if (!isActive)
                                {
                                    await MainThread.InvokeOnMainThreadAsync(async () =>
                                    {
                                        if (CurrentUser != null)
                                        {
                                            CurrentUser = null;
                                            await MainPage.DisplayAlert("Oturum Sonlandı", "Oturumunuz sonlandırıldı.", "Tamam");
                                            var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
                                            MainPage = new NavigationPage(loginPage);
                                        }
                                    });
                                    break;
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        // Hata olsa bile çökme, sadece logla ve devam et (veya bekle)
                        System.Diagnostics.Debug.WriteLine($"Session Check Error (Ignored): {ex.Message}");
                        // Hata sonrası biraz daha uzun bekle ki sürekli hata fırlatmasın
                        await Task.Delay(5000);
                    }
                }
            });
        }

        private void StartBackgroundTasks()
        {
            // Eski görevleri iptal et
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            // 1. Oturum Kontrol Döngüsü Başlat
            Task.Run(() => SessionCheckLoop(token));

            // 2. Dosya Senkronizasyon Döngüsü Başlat (Sadece Adminler İçin)
            Task.Run(() => FileSyncLoop(token));
        }
        private async Task SessionCheckLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, token); // 5 saniyede bir kontrol

                    // İnternet yoksa kontrol yapma (Çökmemesi için)
                    if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) continue;

                    if (CurrentUser != null)
                    {
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
                            bool isActive = await authService.IsUserActiveAsync(CurrentUser.Id);

                            if (!isActive)
                            {
                                await MainThread.InvokeOnMainThreadAsync(async () =>
                                {
                                    if (CurrentUser != null)
                                    {
                                        CurrentUser = null;
                                        await MainPage.DisplayAlert("Oturum Sonlandı", "Oturumunuz sonlandırıldı.", "Tamam");
                                        await PerformLocalLogoutAsync();
                                    }
                                });
                                break; // Döngüden çık
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Session Check Error: {ex.Message}");
                    await Task.Delay(5000); // Hata alırsa biraz bekle
                }
            }
        }

        private async Task FileSyncLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 1 Dakikada bir kontrol et (Süreyi ihtiyaca göre değiştirebilirsin)
                    await Task.Delay(TimeSpan.FromMinutes(1), token);

                    // İnternet yoksa veya kullanıcı yoksa veya ADMIN değilse yapma
                    if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) continue;
                    if (CurrentUser == null || !CurrentUser.IsAdmin) continue;

                    // Senkronizasyonu Başlat
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();

                        // Bu metot eksik dosyaları indirir (zaten varsa indirmez, hızlı çalışır)
                        await jobService.SyncAllFilesFromFtpAsync();
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Auto-Sync Error: {ex.Message}");
                    await Task.Delay(10000); // Hata alırsa 10sn bekle
                }
            }
        }

        private void Current_ConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            if (e.NetworkAccess != NetworkAccess.Internet)
            {
                // İnternet GİTTİ -> Kullanıcıyı Login'e at
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (CurrentUser != null)
                    {
                        // İsteğe bağlı uyarı:
                        // await MainPage.DisplayAlert("Bağlantı", "İnternet bağlantısı kesildi. Güvenli çıkış yapılıyor.", "Tamam");
                        await PerformLocalLogoutAsync();
                    }
                });
            }
            else
            {
                // İnternet GELDİ -> Adminse hemen bir sync başlat
                if (CurrentUser != null && CurrentUser.IsAdmin)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            using (var scope = _serviceProvider.CreateScope())
                            {
                                var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
                                await jobService.SyncAllFilesFromFtpAsync();
                            }
                        }
                        catch { }
                    });
                }
            }
        }

        private async Task PerformLocalLogoutAsync()
        {
            _cancellationTokenSource?.Cancel(); // Arka plan görevlerini durdur
            CurrentUser = null; // Kullanıcıyı düşür

            // Login sayfasına yönlendir
            var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
            MainPage = new NavigationPage(loginPage);
        }

        private async Task PerformLogoutAsync()
        {
            if (CurrentUser == null) return;

            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
                    await authService.LogoutAsync(CurrentUser.Id);
                }
            }
            catch { }

            CurrentUser = null;
            _cancellationTokenSource?.Cancel(); // Arka plan kontrollerini durdur

            var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
            MainPage = new NavigationPage(loginPage);
        }

        // Eksik dosyaları Hostinger'dan çeken metod
        private async Task SyncMissingFilesAsync()
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    // SyncService henüz yoksa JobService içine veya yeni bir servise ekleyebiliriz.
                    // Şimdilik IJobService üzerinden bir tetikleyici varsayalım veya direkt mantığı buraya kuralım.
                    // En temizi bir FileSyncService oluşturmaktır ama mevcut yapıda JobService'e ekleyeceğim.
                    var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
                    await jobService.SyncAllFilesFromFtpAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sync Hatası: {ex.Message}");
            }
        }

    }
}