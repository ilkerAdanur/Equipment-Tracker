using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EquipmentTracker.Services.Auth;
using EquipmentTracker.Views;
using EquipmentTracker.ViewModels;
using System;
using System.IO;
using System.Threading.Tasks;


namespace EquipmentTracker.ViewModels
{
    public partial class SettingsViewModel : BaseViewModel
    {
        private readonly IFolderPicker _folderPicker;

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
        public bool IsAdminUser => App.CurrentUser?.IsAdmin ?? false;

        public SettingsViewModel(IFolderPicker folderPicker)
        {
            _folderPicker = folderPicker;
            Title = "Ayarlar";
            LoadSettings();
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
            LoadAttachmentPath();

            // Anahtar kontrolü: ServerIP varsa bağlı kabul et
            if (Preferences.ContainsKey("ServerIP"))
            {
                ServerIp = Preferences.Get("ServerIP", "");
                DbUser = Preferences.Get("DbUser", "tracker_user");
                DbPassword = Preferences.Get("DbPassword", "123456");

                SetConnectedState(true);
            }
            else
            {
                // Varsayılan değerleri doldur ama bağlı yapma
                ServerIp = "192.168.1.20"; // Örnek IP
                DbUser = "tracker_user";
                DbPassword = "123";

                SetConnectedState(false);
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


        [RelayCommand]
        async Task SaveServerIp()
        {
            if (string.IsNullOrWhiteSpace(ServerIp))
            {
                await Shell.Current.DisplayAlert("Hata", "IP adresi boş olamaz.", "Tamam");
                return;
            }

            // Ayarları Kaydet
            Preferences.Set("ServerIP", ServerIp);
            Preferences.Set("DbUser", DbUser);
            Preferences.Set("DbPassword", DbPassword);

            // Durumu güncelle
            SetConnectedState(true);

            await Shell.Current.DisplayAlert("Başarılı", "Bağlantı ayarları kaydedildi ve aktifleşti.", "Tamam");
        }


        [RelayCommand]
        async Task Disconnect()
        {
            bool answer = await Shell.Current.DisplayAlert("Bağlantıyı Kes",
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

            await Shell.Current.DisplayAlert("Bilgi", "Bağlantı kesildi. Yeni ayar girebilirsiniz.", "Tamam");
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
            if (string.IsNullOrWhiteSpace(AttachmentPath))
            {
                await Shell.Current.DisplayAlert("Hata", "Dosya yolu boş olamaz.", "Tamam");
                return;
            }

            if (IsBusy) return;
            IsBusy = true;

            // Klasörü oluşturmayı dene (izin kontrolü için)
            try
            {
                if (!Directory.Exists(Path.Combine(AttachmentPath, "Attachments")))
                {
                    Directory.CreateDirectory(Path.Combine(AttachmentPath, "Attachments"));
                }

                // Yolu kaydet
                Preferences.Set("attachment_path", AttachmentPath);
                await Shell.Current.DisplayAlert("Başarılı", "Yeni dosya yolu kaydedildi.", "Tamam");
            }
            catch (Exception ex)
            {
                // Genellikle 'C:\' gibi izin olmayan bir yere kaydetmeye çalışınca bu hata alınır.
                await Shell.Current.DisplayAlert("Hata", $"Yol kaydedilemedi. Geçerli bir klasör olduğundan emin olun.\n\nHata: {ex.Message}", "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }


    }
}