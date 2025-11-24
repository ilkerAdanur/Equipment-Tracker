using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EquipmentTracker.Services.Auth;
using EquipmentTracker.Services.Job;
using EquipmentTracker.ViewModels;
using EquipmentTracker.Views;
using Microsoft.Data.SqlClient;
using System;
using System.IO;
using System.Threading.Tasks;


namespace EquipmentTracker.ViewModels
{
    public partial class SettingsViewModel : BaseViewModel
    {
        private readonly IFolderPicker _folderPicker;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        bool _isAdmin;

        [ObservableProperty]
        string _attachmentPath;
        [ObservableProperty]
        string _serverIp;

        [ObservableProperty]
        string _dbUser = "tracker_user"; // Varsayılan değer
        [ObservableProperty]
        string _dbPassword = "123456";


        [ObservableProperty]
        bool _isConnected;

        [ObservableProperty]
        bool _isInputsEnabled;

        // Ekranda görünecek Durum Metni ve Rengi
        [ObservableProperty]
        string _statusText;

        [ObservableProperty]
        Color _statusColor;
        [ObservableProperty]
        bool _isUserLoggedIn;

        public bool IsAdminUser => App.CurrentUser?.IsAdmin ?? false;

        public SettingsViewModel(IFolderPicker folderPicker, IServiceProvider serviceProvider)
        {
            _folderPicker = folderPicker;
            _serviceProvider = serviceProvider;
            Title = "Ayarlar";

            // Kullanıcı admin mi kontrol et
            IsAdmin = App.CurrentUser?.IsAdmin ?? false;

            LoadSettings();
        }

        public void RefreshUserStatus()
        {
            IsUserLoggedIn = App.CurrentUser != null;

            // Eğer kullanıcı yöneticiyse, Admin ayarlarını da aç/kapa
            IsAdmin = App.CurrentUser?.IsAdmin ?? false;
        }

        /// <summary>
        /// Kayıtlı yolu Preferences'tan okur ve ekrandaki Entry'ye yazar.
        /// </summary>
        private void LoadAttachmentPath()
        {
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase");
            AttachmentPath = Preferences.Get("attachment_path", defaultPath);
        }

        private async void LoadSettings() // void -> async void yaptık
        {
            // IP Ayarlarını Yükle (Bunlar yerel kalmalı ki sunucuyu bulabilsin)
            if (Preferences.ContainsKey("ServerIP"))
            {
                ServerIp = Preferences.Get("ServerIP", "");
                DbUser = Preferences.Get("DbUser", "tracker_user");
                DbPassword = Preferences.Get("DbPassword", "123456");
                SetConnectedState(true);

                // BAĞLANDIYSA: Dosya yolunu veritabanından çek
                await LoadGlobalAttachmentPath();
            }
            else
            {
                // Varsayılanlar
                ServerIp = "192.168.1.20";
                DbUser = "tracker_user";
                DbPassword = "123";
                SetConnectedState(false);
            }
        }
        private async Task LoadGlobalAttachmentPath()
        {
            try
            {
                // ServiceProvider üzerinden servise erişiyoruz (Constructor'da eklemeniz gerekebilir veya Handler üzerinden)
                // Basitlik için Handler kullanıyoruz:
                var jobService = Application.Current.Handler.MauiContext.Services.GetService<IJobService>();
                var globalPath = await jobService.GetGlobalAttachmentPathAsync();

                if (!string.IsNullOrEmpty(globalPath))
                {
                    AttachmentPath = globalPath;
                }
                else
                {
                    AttachmentPath = "Henüz ayarlanmamış (Admin bekleniyor)";
                }
            }
            catch
            {
                AttachmentPath = "Veritabanına erişilemedi.";
            }
        }
        private void SetConnectedState(bool connected)
        {
            IsConnected = connected;
            IsInputsEnabled = !connected;

            if (connected)
            {
                StatusText = "DURUM: BAĞLI (Aktif)";
                StatusColor = Colors.Green;
            }
            else
            {
                StatusText = "DURUM: BAĞLANTI YOK";
                StatusColor = Colors.Red;
            }

            // --- YENİ EKLENEN KISIM: Tüm uygulamaya haber ver ---
            WeakReferenceMessenger.Default.Send(new ConnectionMessage(connected));
        }

        [RelayCommand]
        async Task Logout()
        {
            bool answer = await Shell.Current.DisplayAlert("Çıkış", "Oturumu kapatmak istiyor musunuz?", "Evet", "Hayır");
            if (!answer) return;

            // YENİ: Veritabanında Offline yap
            if (App.CurrentUser != null)
            {
                // Servisi bul ve logout yap
                var authService = Application.Current.Handler.MauiContext.Services.GetService<IAuthService>();
                await authService.LogoutAsync(App.CurrentUser.Id);
            }

            // Kullanıcıyı sıfırla
            App.CurrentUser = null;

            // Login sayfasına dön
            var loginPage = Application.Current.Handler.MauiContext.Services.GetService<LoginPage>();
            Application.Current.MainPage = new NavigationPage(loginPage);
        }


        private async Task ShowAlertAsync(string title, string message)
        {
            // UI işlemlerini garanti altına almak için MainThread kullanıyoruz
            if (Application.Current?.MainPage != null)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Application.Current.MainPage.DisplayAlert(title, message, "Tamam");
                });
            }
        }

        [RelayCommand]
        async Task SaveServerIp()
        {
            if (string.IsNullOrWhiteSpace(ServerIp))
            {
                await ShowAlertAsync("Hata", "IP adresi boş olamaz.");
                return;
            }

            if (IsBusy) return;
            IsBusy = true;

            // Ayarları şimdiden kaydedelim ki JobService (DataContext) bunları kullanabilsin
            Preferences.Set("ServerIP", ServerIp);
            Preferences.Set("DbUser", DbUser);
            Preferences.Set("DbPassword", DbPassword);

            try
            {
                var builder = new SqlConnectionStringBuilder
                {
                    DataSource = ServerIp,
                    InitialCatalog = "TrackerDB",
                    UserID = DbUser,
                    Password = DbPassword,
                    TrustServerCertificate = true,
                    ConnectTimeout = 5
                };

                // 1. Bağlantıyı Test Et
                using (var connection = new SqlConnection(builder.ConnectionString))
                {
                    await connection.OpenAsync();
                }

                // --- BAĞLANTI BAŞARILIYSA ---
                SetConnectedState(true);
                if (IsConnected) await LoadGlobalAttachmentPath();
                await ShowAlertAsync("Başarılı", "Bağlantı sağlandı ve ayarlar kaydedildi.");
            }
            catch (SqlException ex)
            {
                // *** ÖZEL DURUM: Veritabanı Yok Hatası (Hata Kodu 4060) ***
                if (ex.Number == 4060)
                {
                    try
                    {
                        // Kullanıcıya sormadan veya sorarak otomatik kur
                        // "Veritabanı bulunamadı. Otomatik oluşturuluyor..." gibi bir mesaj (Toast) verilebilir.

                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();

                            // Veritabanını ve Tabloları Oluştur + Admin Ekle
                            await jobService.InitializeDatabaseAsync();
                        }

                        // Tekrar bağlanmayı dene (Teyit amaçlı)
                        SetConnectedState(true);
                        await LoadGlobalAttachmentPath();

                        await ShowAlertAsync("Kurulum Tamamlandı",
                            "Veritabanı bulunamadığı için otomatik olarak oluşturuldu ve varsayılan veriler (Admin) eklendi. Bağlantı başarılı.");
                    }
                    catch (Exception createEx)
                    {
                        SetConnectedState(false);
                        await ShowAlertAsync("Kurulum Hatası", $"Veritabanı oluşturulurken hata çıktı: {createEx.Message}");
                    }
                }
                else
                {
                    // Diğer SQL hataları (Şifre yanlış, Sunucu kapalı vs.)
                    SetConnectedState(false);
                    await ShowAlertAsync("Bağlantı Hatası", $"Sunucuya bağlanılamadı.\nHata Kodu: {ex.Number}\nMesaj: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                SetConnectedState(false);
                await ShowAlertAsync("Hata", $"Beklenmedik bir hata: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }


        [RelayCommand]
        async Task Disconnect()
        {
            if (Application.Current?.MainPage == null) return;

            bool answer = await Application.Current.MainPage.DisplayAlert("Bağlantıyı Kes",
                "Sunucu bağlantısını kesmek istiyor musunuz?",
                "Evet, Kes", "İptal");

            if (!answer) return;

            // Ayarları Sil
            Preferences.Remove("ServerIP");
            Preferences.Remove("DbUser");
            Preferences.Remove("DbPassword");

            // Ekranı Temizle (İsteğe bağlı, varsayılanları geri getirebiliriz)
            ServerIp = string.Empty;
            DbUser = string.Empty;
            DbPassword = string.Empty;

            // Durumu güncelle
            SetConnectedState(false);

            await Application.Current.MainPage.DisplayAlert("Bilgi", "Bağlantı kesildi. Yeni ayar girebilirsiniz.", "Tamam");
        
        }

        /// <summary>
        /// Kullanıcıya klasör seçtirir.
        /// </summary>
        [RelayCommand]
        async Task SelectAttachmentPath(CancellationToken cancellationToken)
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var result = await _folderPicker.PickAsync(cancellationToken);
                if (result.IsSuccessful)
                {
                    // Seçilen klasörün yolunu Entry'ye yaz
                    AttachmentPath = result.Folder.Path;

                    // Yolu otomatik olarak kaydet
                    await SaveAttachmentPath();
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Klasör seçilemedi: {ex.Message}", "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Ekrana girilen yeni yolu Preferences'a kaydeder.
        /// </summary>
        [RelayCommand]
        async Task SaveAttachmentPath()
        {
            if (!IsAdminUser) // IsAdminUser property'niz zaten vardı
            {
                await ShowAlertAsync("Yetkisiz", "Bu ayarı sadece yöneticiler değiştirebilir.");
                return;
            }

            if (string.IsNullOrWhiteSpace(AttachmentPath)) return;

            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // 1. Erişim Testi (Klasör oluşturmayı dene)
                string testPath = Path.Combine(AttachmentPath, "Attachments");
                if (!Directory.Exists(testPath))
                {
                    Directory.CreateDirectory(testPath);
                }

                // 2. Veritabanına Kaydet
                var jobService = Application.Current.Handler.MauiContext.Services.GetService<IJobService>();
                await jobService.SetGlobalAttachmentPathAsync(AttachmentPath);

                await ShowAlertAsync("Başarılı", "Dosya yolu kaydedildi ve tüm kullanıcılara yansıtıldı.");
            }
            catch (UnauthorizedAccessException)
            {
                await ShowAlertAsync("Erişim Hatası", "Uygulamanın bu klasöre yazma izni yok. Lütfen klasör özelliklerinden 'Everyone' için 'Tam Denetim' verin.");
            }
            catch (Exception ex)
            {
                await ShowAlertAsync("Hata", $"Klasör hatası: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

    }
}