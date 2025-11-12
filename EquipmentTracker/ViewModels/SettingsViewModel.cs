using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

        public SettingsViewModel(IFolderPicker folderPicker)
        {
            _folderPicker = folderPicker;
            Title = "Ayarlar";
            LoadAttachmentPath(); // Sayfa açılırken mevcut yolu yükle
        }

        /// <summary>
        /// Kayıtlı yolu Preferences'tan okur ve ekrandaki Entry'ye yazar.
        /// </summary>
        private void LoadAttachmentPath()
        {
            // 'Belgelerim\TrackerDatabase'i varsayılan yol olarak ayarla
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase");
            // "attachment_path" anahtarıyla kayıtlı yolu getir, yoksa varsayılanı kullan
            AttachmentPath = Preferences.Get("attachment_path", defaultPath);
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