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
            Connectivity.Current.ConnectivityChanged += Current_ConnectivityChanged;
        }

        protected override Window CreateWindow(IActivationState activationState)
        {
            var window = base.CreateWindow(activationState);

            const int WindowWidth = 650;
            const int WindowHeight = 700;

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
        public void StartSessionCheck()
        {
            // Varsa eskisini durdur
            StopSessionCheck();

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            // 1. Oturum Kontrol Döngüsü
            Task.Run(() => SessionCheckLoop(token));

            // 2. Dosya Senkronizasyonu (Sadece Admin ise)
            if (CurrentUser != null && CurrentUser.IsAdmin)
            {
                Task.Run(() => FileSyncLoop(token));
            }
        }
        public void StopSessionCheck()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = null;
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
                    // 2 dakikada bir sinyal gönder (MySQL Event 5 dk demiştik, bu süre güvenli)
                    await Task.Delay(TimeSpan.FromMinutes(2), token);

                    if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) continue;
                    if (CurrentUser == null) continue;

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

                        // YENİ: Kalp atışı gönder (LastActive tarihini güncelle)
                        await authService.UpdateLastActiveAsync(CurrentUser.Id);

                        // İsteğe bağlı: Hala başkası tarafından atılıp atılmadığını kontrol et
                        bool isActive = await authService.IsUserActiveAsync(CurrentUser.Id);

                        if (!isActive)
                        {
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                            {
                                // Çıkış yapılmışsa at
                                StopSessionCheck();
                                CurrentUser = null;
                                await MainPage.DisplayAlert("Oturum Sonlandı", "Oturumunuz sonlandırıldı.", "Tamam");
                                MainPage = new NavigationPage(_serviceProvider.GetRequiredService<LoginPage>());
                            });
                            break;
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(5000); }
            }
        }

        private async Task FileSyncLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), token);
                    if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) continue;
                    if (CurrentUser == null || !CurrentUser.IsAdmin) break; // Admin değilse döngüyü kır

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
                        await jobService.SyncAllFilesFromFtpAsync();
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }

        private void Current_ConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            if (e.NetworkAccess != NetworkAccess.Internet)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (CurrentUser != null)
                    {
                        StopSessionCheck(); // Döngüyü durdur
                        CurrentUser = null;
                        MainPage = new NavigationPage(_serviceProvider.GetRequiredService<LoginPage>());
                    }
                });
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