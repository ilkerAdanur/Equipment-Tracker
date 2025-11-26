using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EquipmentTracker.Services.Auth;
using EquipmentTracker.Services.Job;
using EquipmentTracker.ViewModels;
using EquipmentTracker.Views;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;


namespace EquipmentTracker.ViewModels
{
    public partial class SettingsViewModel : BaseViewModel
    {
        private readonly IFolderPicker _folderPicker;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAdminUser))] 
        bool _isAdmin;

        [ObservableProperty]
        string _attachmentPath;

        [ObservableProperty]
        string _serverIp;

        [ObservableProperty]
        string _dbName; 

        [ObservableProperty]
        string _dbUser;

        [ObservableProperty]
        string _dbPassword;

        [ObservableProperty]
        string _ftpHost = "ftp://46.202.156.198";

        [ObservableProperty]
        string _ftpUser; 

        [ObservableProperty]
        string _ftpPassword;

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

        [ObservableProperty]
        bool _isFtpConnected;

        [ObservableProperty]
        bool _isFtpInputsEnabled;

        public bool IsAdminUser => App.CurrentUser?.IsAdmin ?? false;

        public SettingsViewModel(IFolderPicker folderPicker, IServiceProvider serviceProvider)
        {
            _folderPicker = folderPicker;
            _serviceProvider = serviceProvider;
            Title = "Ayarlar";
            IsAdmin = App.CurrentUser?.IsAdmin ?? false; 
            LoadSettings();
        }

        public void RefreshUserStatus()
        {
            IsUserLoggedIn = App.CurrentUser != null;
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

        private void LoadSettings()
        {
            // 1. Veritabanı Ayarlarını Yükle
            if (Preferences.ContainsKey("ServerIP"))
            {
                ServerIp = Preferences.Get("ServerIP", "");
                DbName = Preferences.Get("DbName", "");
                DbUser = Preferences.Get("DbUser", "");
                DbPassword = Preferences.Get("DbPassword", "");
                SetConnectedState(true);
            }
            else
            {
                ServerIp = "";
                DbName = "";
                DbUser = "";
                DbPassword = "";
                SetConnectedState(false);
            }

            // 2. FTP Ayarlarını Yükle
            if (Preferences.ContainsKey("FtpHost"))
            {
                FtpHost = Preferences.Get("FtpHost", "");
                FtpUser = Preferences.Get("FtpUser", "");
                FtpPassword = Preferences.Get("FtpPassword", "");
                SetFtpConnectedState(true);
            }
            else
            {
                FtpHost = "ftp://46.202.xxx.xxx"; // Varsayılan
                FtpUser = "";
                FtpPassword = "";
                SetFtpConnectedState(false);
            }

            // 3. Yerel Dosya Yolu
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase");
            AttachmentPath = Preferences.Get("attachment_path", defaultPath);
        }
        [RelayCommand]
        async Task ConnectDatabase()
        {
            // Boş alan kontrolü
            if (string.IsNullOrWhiteSpace(ServerIp) || string.IsNullOrWhiteSpace(DbName) || string.IsNullOrWhiteSpace(DbUser))
            {
                await ShowAlertAsync("Hata", "Lütfen veritabanı bilgilerini eksiksiz doldurun.");
                return;
            }

            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // 1. MySQL Bağlantı Cümlesini Oluştur (Hostinger Uyumlu)
                // DİKKAT: SqlConnectionStringBuilder DEĞİL, MySqlConnectionStringBuilder kullanıyoruz.
                var builder = new MySqlConnectionStringBuilder
                {
                    Server = ServerIp,
                    Database = DbName,
                    UserID = DbUser,
                    Password = DbPassword,
                    Port = 3306, // MySQL varsayılan portu
                    ConnectionTimeout = 30 // Zaman aşımı 30 saniye
                };

                // 2. Bağlantıyı Test Et
                // DİKKAT: SqlConnection DEĞİL, MySqlConnection kullanıyoruz.
                using (var connection = new MySqlConnection(builder.ConnectionString))
                {
                    await connection.OpenAsync();
                }

                // 3. Başarılıysa Ayarları Kaydet
                Preferences.Set("ServerIP", ServerIp);
                Preferences.Set("DbName", DbName);
                Preferences.Set("DbUser", DbUser);
                Preferences.Set("DbPassword", DbPassword);

                SetConnectedState(true);
                await ShowAlertAsync("Başarılı", "Hostinger (MySQL) bağlantısı başarıyla sağlandı.");
            }
            catch (MySqlException ex)
            {
                SetConnectedState(false);
                // MySQL'e özgü hataları yakalar
                await ShowAlertAsync("MySQL Hatası", $"Bağlanılamadı.\nHata Kodu: {ex.Number}\nMesaj: {ex.Message}");
            }
            catch (Exception ex)
            {
                SetConnectedState(false);
                await ShowAlertAsync("Genel Hata", $"Bağlantı hatası: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }


        [RelayCommand]
        async Task DisconnectDatabase()
        {
            bool answer = await ShowConfirmAsync("Bağlantıyı Kes", "Veritabanı ayarlarını silmek istiyor musunuz?");
            if (!answer) return;

            Preferences.Remove("ServerIP");
            Preferences.Remove("DbUser");
            Preferences.Remove("DbPassword");

            SetConnectedState(false);
        }

        [RelayCommand]
        async Task SaveServerIp()
        {
            if (string.IsNullOrWhiteSpace(ServerIp) || string.IsNullOrWhiteSpace(DbName) || string.IsNullOrWhiteSpace(DbUser))
            {
                await ShowAlertAsync("Hata", "Lütfen veritabanı alanlarını eksiksiz doldurun.");
                return;
            }

            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // 1. MySQL Bağlantı Testi (Hostinger)
                var builder = new MySqlConnectionStringBuilder
                {
                    Server = ServerIp,
                    Database = DbName,
                    UserID = DbUser,
                    Password = DbPassword,
                    Port = 3306,
                    ConnectionTimeout = 30
                };

                using (var conn = new MySqlConnection(builder.ConnectionString))
                {
                    await conn.OpenAsync();
                }

                // 2. FTP Bağlantı Testi
                if (!string.IsNullOrWhiteSpace(FtpHost) && !string.IsNullOrWhiteSpace(FtpUser) && !string.IsNullOrWhiteSpace(FtpPassword))
                {
                    try
                    {
                        // ftp:// ön eki yoksa ekle
                        string ftpUrl = FtpHost.StartsWith("ftp://") ? FtpHost : "ftp://" + FtpHost;

                        FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                        request.Method = WebRequestMethods.Ftp.ListDirectory;
                        request.Credentials = new NetworkCredential(FtpUser, FtpPassword);
                        request.Timeout = 10000; // 10 saniye zaman aşımı

                        using (FtpWebResponse response = (FtpWebResponse)await request.GetResponseAsync())
                        {
                            // Hata vermediyse bağlantı başarılıdır
                        }
                    }
                    catch (Exception ftpEx)
                    {
                        // Veritabanı bağlandı ama FTP hatası var, kullanıcıyı uyar
                        throw new Exception($"MySQL bağlandı ancak FTP hatası alındı: {ftpEx.Message}");
                    }
                }

                // 3. Her şey yolundaysa tüm ayarları kaydet
                Preferences.Set("ServerIP", ServerIp);
                Preferences.Set("DbName", DbName);
                Preferences.Set("DbUser", DbUser);
                Preferences.Set("DbPassword", DbPassword);

                // FTP Ayarlarını Kaydet
                Preferences.Set("FtpHost", FtpHost);
                Preferences.Set("FtpUser", FtpUser);
                Preferences.Set("FtpPassword", FtpPassword);

                SetConnectedState(true);
                await ShowAlertAsync("Başarılı", "Veritabanı ve FTP bağlantısı başarıyla sağlandı ve kaydedildi.");
            }
            catch (MySqlException ex)
            {
                SetConnectedState(false);
                await ShowAlertAsync("MySQL Hatası", $"Veritabanına bağlanılamadı.\nHata: {ex.Message}");
            }
            catch (Exception ex)
            {
                SetConnectedState(false);
                await ShowAlertAsync("Hata", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        async Task ConnectFtp()
        {
            if (string.IsNullOrWhiteSpace(FtpHost) || string.IsNullOrWhiteSpace(FtpUser))
            {
                await ShowAlertAsync("Hata", "FTP bilgileri eksik.");
                return;
            }

            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // FTP Testi
                string ftpUrl = FtpHost.StartsWith("ftp://") ? FtpHost : "ftp://" + FtpHost;
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = new NetworkCredential(FtpUser, FtpPassword);
                request.Timeout = 5000;

                using (FtpWebResponse response = (FtpWebResponse)await request.GetResponseAsync()) { }

                // Başarılıysa kaydet
                Preferences.Set("FtpHost", FtpHost);
                Preferences.Set("FtpUser", FtpUser);
                Preferences.Set("FtpPassword", FtpPassword);

                SetFtpConnectedState(true);
                await ShowAlertAsync("Başarılı", "FTP bağlantısı sağlandı.");
            }
            catch (Exception ex)
            {
                SetFtpConnectedState(false);
                await ShowAlertAsync("Hata", $"FTP hatası: {ex.Message}");
            }
            finally { IsBusy = false; }
        }


        [RelayCommand]
        async Task DisconnectFtp()
        {
            bool answer = await ShowConfirmAsync("Bağlantıyı Kes", "FTP ayarlarını silmek istiyor musunuz?");
            if (!answer) return;

            Preferences.Remove("FtpHost");
            Preferences.Remove("FtpUser");
            Preferences.Remove("FtpPassword");

            SetFtpConnectedState(false);
        }

        private void SetFtpConnectedState(bool connected)
        {
            IsFtpConnected = connected;
            IsFtpInputsEnabled = !connected;
        }

        private async Task LoadGlobalAttachmentPath()
        {
            try
            {
                // DÜZELTME: Application.Current... yerine güvenli olan _serviceProvider kullanıyoruz.
                // Bu sayede NullReference hatasından kurtulursunuz.
                using (var scope = _serviceProvider.CreateScope())
                {
                    var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Path Yükleme Hatası: {ex.Message}");
                AttachmentPath = "Veritabanına erişilemedi veya yol bulunamadı.";
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
            WeakReferenceMessenger.Default.Send(new ConnectionMessage(connected));
        }

        [RelayCommand]
        async Task Logout()
        {
            bool answer = await ShowConfirmAsync("Çıkış", "Oturumu kapatmak istiyor musunuz?");
            if (!answer) return;

            if (App.CurrentUser != null)
            {
                try
                {
                    var authService = Application.Current.Handler.MauiContext.Services.GetService<IAuthService>();
                    await authService.LogoutAsync(App.CurrentUser.Id);
                }
                catch { }
            }

            App.CurrentUser = null;
            var loginPage = Application.Current.Handler.MauiContext.Services.GetService<LoginPage>();
            Application.Current.MainPage = new NavigationPage(loginPage);
        }

        private async Task ShowAlertAsync(string title, string message)
        {
            // Application.Current null ise işlem yapma (Çökmeyi önler)
            if (Application.Current != null && Application.Current.MainPage != null)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Application.Current.MainPage.DisplayAlert(title, message, "Tamam");
                });
            }
        }

        private async Task<bool> ShowConfirmAsync(string title, string message)
        {
            if (Application.Current?.MainPage != null)
            {
                return await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    return await Application.Current.MainPage.DisplayAlert(title, message, "Evet", "İptal");
                });
            }
            return false;
        }

        [RelayCommand]
        async Task SaveAttachmentPath()
        {
            if (!IsAdminUser)
            {
                await ShowAlertAsync("Unauthorized", "Only admins can change this setting.");
                return;
            }

            if (string.IsNullOrWhiteSpace(AttachmentPath)) return;

            if (IsBusy) return;
            IsBusy = true;

            try
            {
                string testPath = Path.Combine(AttachmentPath, "Attachments");
                if (!Directory.Exists(testPath)) Directory.CreateDirectory(testPath);

                Preferences.Set("attachment_path", AttachmentPath);

                // Update Global Setting in DB
                // Use _serviceProvider to avoid NullReferenceException
                using (var scope = _serviceProvider.CreateScope())
                {
                    var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
                    await jobService.SetGlobalAttachmentPathAsync(AttachmentPath);

                    // Start Sync
                    _ = Task.Run(async () =>
                    {
                        try { await jobService.SyncAllFilesFromFtpAsync(); }
                        catch { }
                    });
                }

                await ShowAlertAsync("Success", "Path saved. Synchronization started...");
            }
            catch (Exception ex)
            {
                await ShowAlertAsync("Error", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }


        [RelayCommand]
        async Task Disconnect()
        {
            bool answer = await Shell.Current.DisplayAlert("Bağlantıyı Kes", "Tüm bağlantı ayarlarını silmek istiyor musunuz?", "Evet", "İptal");
            if (!answer) return;

            Preferences.Remove("ServerIP");
            Preferences.Remove("DbName");
            Preferences.Remove("DbUser");
            Preferences.Remove("DbPassword");

            // FTP'yi de sil
            Preferences.Remove("FtpHost");
            Preferences.Remove("FtpUser");
            Preferences.Remove("FtpPassword");

            SetConnectedState(false);
        }

        /// <summary>
        /// Kullanıcıya klasör seçtirir.
        /// </summary>
        [RelayCommand]
        async Task SelectAttachmentPath(CancellationToken cancellationToken)
        {
            try
            {
                var result = await _folderPicker.PickAsync(cancellationToken);
                if (result.IsSuccessful)
                {
                    AttachmentPath = result.Folder.Path;
                    // Otomatik kaydetme isteğe bağlı, butona basınca kaydetmesi daha güvenli
                }
            }
            catch (Exception ex) { await ShowAlertAsync("Hata", ex.Message); }
        }

       
       }
}