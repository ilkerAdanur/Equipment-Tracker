using EquipmentTracker.Models;
using EquipmentTracker.Services.Auth;
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
            _cancellationTokenSource?.Cancel(); // Kontrolü durdur

            if (CurrentUser != null)
            {
                var authService = _serviceProvider.GetService<IAuthService>();
                // Senkron bekleme ile çıkış yap
                Task.Run(async () => await authService.LogoutAsync(CurrentUser.Id)).Wait();
            }
        }

        // --- YENİ: OTURUM KONTROL MEKANİZMASI ---
        private void StartSessionCheck()
        {
            // Eski varsa iptal et
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            // Arka planda sonsuz döngü başlat
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // 5 saniyede bir kontrol et
                        await Task.Delay(5000, token);

                        // Eğer giriş yapmış bir kullanıcı varsa
                        if (CurrentUser != null)
                        {
                            // Servisi oluştur (Scope kullanarak veritabanı çakışmasını önle)
                            using (var scope = _serviceProvider.CreateScope())
                            {
                                var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

                                // Veritabanından "IsOnline" durumunu sorgula
                                bool isActive = await authService.IsUserActiveAsync(CurrentUser.Id);

                                // Eğer Admin bağlantıyı kestiyse (isActive = false)
                                if (!isActive)
                                {
                                    // Ana Thread'e geç ve at
                                    await MainThread.InvokeOnMainThreadAsync(async () =>
                                    {
                                        // Tekrar kontrol (Çakışma olmasın)
                                        if (CurrentUser != null)
                                        {
                                            CurrentUser = null; // Kullanıcıyı düşür

                                            await MainPage.DisplayAlert("Oturum Sonlandı",
                                                "Yönetici tarafından bağlantınız kesildi.", "Tamam");

                                            // Login sayfasına yönlendir
                                            var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
                                            MainPage = new NavigationPage(loginPage);
                                        }
                                    });

                                    // Döngüden çık (zaten çıkış yapıldı)
                                    break;
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Uygulama kapanıyor, normal durum.
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Bağlantı hatası vb. olursa logla ama uygulamayı çökertme
                        System.Diagnostics.Debug.WriteLine($"Session Check Error: {ex.Message}");
                    }
                }
            });
        }
    }
}