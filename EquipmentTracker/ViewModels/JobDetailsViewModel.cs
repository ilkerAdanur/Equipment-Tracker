// Dosya: ViewModels/JobDetailsViewModel.cs
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentTracker.Models;
using EquipmentTracker.Models.Enums; // YENİ ENUM KULLANIMI
using EquipmentTracker.Services;
using EquipmentTracker.Services.AttachmentServices;
using EquipmentTracker.Services.EquipmentPartAttachmentServices;
using EquipmentTracker.Services.EquipmentPartService;
using EquipmentTracker.Services.EquipmentService;
using EquipmentTracker.Services.Job;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using MiniExcelLibs;
using System.Collections.ObjectModel;
using System.Diagnostics; // Hata ayıklama için
using System.IO;
using System.Text.RegularExpressions;

namespace EquipmentTracker.ViewModels
{
    [QueryProperty(nameof(JobId), "JobId")]
    public partial class JobDetailsViewModel : BaseViewModel
    {
        private readonly IJobService _jobService;
        private readonly IEquipmentService _equipmentService;
        private readonly IEquipmentPartService _partService; // Sizin isimlendirmeniz
        private readonly IAttachmentService _attachmentService;
        private readonly IEquipmentPartAttachmentService _equipmentPartAttachmentService;
        private readonly FtpHelper _ftpHelper;
        public ObservableCollection<EquipmentDisplayViewModel> DisplayEquipments { get; set; } = new();
        [ObservableProperty]
        JobModel _currentJob;



        [ObservableProperty]
        int _jobId;


        // --- YENİ ONAY DURUMU ÖZELLİKLERİ ---
        [ObservableProperty]
        bool _isPending; // Onay bekliyorsa

        [ObservableProperty]
        bool _isApproved; // Onaylandıysa

        [ObservableProperty]
        bool _isRejected; // Reddedildiyse

        [ObservableProperty]
        CopyableTextViewModel jobNameDisplay;

        [ObservableProperty]
        CopyableTextViewModel jobOwnerDisplay;

        [ObservableProperty]
        CopyableTextViewModel jobDescriptionDisplay;

        // Constructor (Sizin 5 servisinizi de alıyor)
        public JobDetailsViewModel(IJobService jobService,
                                 IEquipmentService equipmentService,
                                 IEquipmentPartService partService, // Sizin isimlendirmeniz
                                 IAttachmentService attachmentService,
                                 IEquipmentPartAttachmentService equipmentPartAttachmentService,
                                 FtpHelper ftpHelper)
        {
            _jobService = jobService;
            _equipmentService = equipmentService;
            _partService = partService; // Sizin isimlendirmeniz
            _attachmentService = attachmentService;
            _equipmentPartAttachmentService = equipmentPartAttachmentService;
            Title = "İş Detayı";
            _ftpHelper = ftpHelper;
        }

        private string SanitizeFolderName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unknown";
            return Regex.Replace(name, @"[\\/:*?""<>| ]", "_").Trim('_');
        }

        [RelayCommand]
        async Task GoToHome()
        {
            await Shell.Current.GoToAsync("..");
        }
        partial void OnJobIdChanged(int value)
        {
            LoadJobDetailsCommand.Execute(value);
        }
        [RelayCommand]
        async Task CopyToClipboard(object textToCopyObj)
        {
            if (textToCopyObj == null)
                return;

            string? textToCopy = string.Empty;

            // Gelen verinin tipini kontrol et
            if (textToCopyObj is DateTime dateTime)
            {
                // Tarihi istediğimiz formatta string'e çevir
                textToCopy = dateTime.ToString("dd.MM.yyyy");
            }
            else
            {
                // Diğer her şeyi string olarak kabul et
                textToCopy = textToCopyObj.ToString();
            }

            if (string.IsNullOrEmpty(textToCopy))
                return;

            await Clipboard.SetTextAsync(textToCopy);

            var popup = new Popup
            {
                Content = new VerticalStackLayout
                {
                    Padding = 10,
                    BackgroundColor = Colors.Black.WithAlpha(0.8f),
                    Children =
            {
                new Label
                {
                    Text = "Kopyalandı ✔️",
                    TextColor = Colors.White,
                    FontSize = 16,
                    HorizontalOptions = LayoutOptions.Center
                }
            }
                }
            };

            Application.Current.MainPage.ShowPopup(popup);

            await Task.Delay(1500);
            popup.Close();
        }

        [RelayCommand]
        public async Task LoadJobDetailsAsync(int jobId)
        {
            if (jobId == 0) return;
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                CurrentJob = await _jobService.GetJobByIdAsync(jobId);

                if (CurrentJob != null)
                {

                    if (IsAdminUser)
                    {
                        _ = Task.Run(DownloadThumbnailsForCurrentJob);
                    }

                    JobNameDisplay = new CopyableTextViewModel(CurrentJob.JobName);
                    JobOwnerDisplay = new CopyableTextViewModel(CurrentJob.JobOwner);
                    JobDescriptionDisplay = new CopyableTextViewModel(CurrentJob.JobDescription);
                    UpdateApprovalStatus();
                }
                else
                {
                    Title = "Detay Bulunamadı";
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "İş detayları yüklenemedi: " + ex.Message, "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        async Task UpdateAttachment(EquipmentAttachment attachment)
        {
            if (attachment == null || CurrentJob == null) return;
            if (!IsAdminUser) return; // Sadece Admin

            try
            {
                // Dosya seç
                var fileResult = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Yeni dosyayı seçin (Eskisi ile değiştirilecek)"
                });

                if (fileResult == null) return;

                if (IsBusy) return;
                IsBusy = true;

                // Ekipmanı bul (Parent bilgisi için)
                var parentEquipment = CurrentJob.Equipments.FirstOrDefault(e => e.Attachments.Contains(attachment));
                if (parentEquipment == null) return;

                // Servisi çağır
                await _attachmentService.UpdateAttachmentAsync(attachment, CurrentJob, parentEquipment, fileResult);

                // UI otomatik güncellenir çünkü attachment nesnesi ObservableObject
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Güncelleme hatası: {ex.Message}", "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // PARÇA DOSYASI GÜNCELLEME
        [RelayCommand]
        async Task UpdatePartAttachment(EquipmentPartAttachment attachment)
        {
            if (attachment == null || CurrentJob == null) return;
            if (!IsAdminUser) return;

            try
            {
                var fileResult = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Yeni dosyayı seçin (Eskisi ile değiştirilecek)"
                });

                if (fileResult == null) return;

                if (IsBusy) return;
                IsBusy = true;

                // Parçayı ve Ekipmanı bul
                Equipment parentEquipment = null;
                EquipmentPart parentPart = null;

                foreach (var eq in CurrentJob.Equipments)
                {
                    var part = eq.Parts.FirstOrDefault(p => p.Attachments.Contains(attachment));
                    if (part != null)
                    {
                        parentEquipment = eq;
                        parentPart = part;
                        break;
                    }
                }

                if (parentEquipment == null || parentPart == null) return;

                await _equipmentPartAttachmentService.UpdateAttachmentAsync(attachment, CurrentJob, parentEquipment, parentPart, fileResult);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Güncelleme hatası: {ex.Message}", "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }
        public bool IsAdminUser => App.CurrentUser?.IsAdmin ?? false;
        private async Task DownloadThumbnailsForCurrentJob()
        {
            if (CurrentJob == null) return;

            try
            {
                // 1. Yerel "Images" Klasörünün Yolunu Bul
                string localImagesBase;
                if (IsAdminUser)
                {
                    // Admin: Kalıcı Klasör
                    string savedPath = Preferences.Get("attachment_path", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase"));
                    localImagesBase = Path.Combine(savedPath, "Attachments", "Images");
                }
                else
                {
                    // User: Geçici Klasör (Cache)
                    localImagesBase = Path.Combine(FileSystem.CacheDirectory, "Images");
                }

                if (!Directory.Exists(localImagesBase)) Directory.CreateDirectory(localImagesBase);

                // 2. Tüm Ekipman ve Parça Resimlerini Kontrol Et
                foreach (var equip in CurrentJob.Equipments)
                {
                    // A. Ekipman Resimleri
                    foreach (var att in equip.Attachments)
                    {
                        await EnsureThumbnailExistsAsync(att, localImagesBase, equip);
                    }

                    // B. Parça Resimleri
                    foreach (var part in equip.Parts)
                    {
                        foreach (var patt in part.Attachments)
                        {
                            await EnsureThumbnailExistsAsync(patt, localImagesBase, equip, part);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Resim İndirme Hatası: {ex.Message}");
            }
        }

        private async Task EnsureThumbnailExistsAsync(dynamic attachment, string localImagesBase, Equipment equip, EquipmentPart part = null)
        {
            string dbPath = attachment.ThumbnailPath;
            string mainFilePath = attachment.FilePath; // Ana dosya yolunu al

            // EĞER: Yol doluysa VE yerel diskte dosya zaten varsa -> İŞLEM YAPMA (Zaten hazır)
            if (!string.IsNullOrEmpty(dbPath) && Path.IsPathRooted(dbPath) && File.Exists(dbPath)) return;

            // EĞER: Yol boşsa AMA dosya DWG değilse -> İŞLEM YAPMA (Thumbnail gerekmez)
            string extension = Path.GetExtension(mainFilePath)?.ToLower();
            if (string.IsNullOrEmpty(dbPath) && extension != ".dwg" && extension != ".dxf") return;

            // --- BURAYA GELDİYSEK YA YOL BOŞTUR (AMA DWG'DİR) YA DA DOSYA YERELDE YOKTUR ---

            // İsimleri hazırla
            string thumbName;
            if (!string.IsNullOrEmpty(dbPath))
            {
                thumbName = Path.GetFileName(dbPath);
            }
            else
            {
                // DB'de yol yoksa, standart isimlendirmeyi varsayalım: "DosyaAdi_thumb.png"
                thumbName = $"{Path.GetFileNameWithoutExtension(mainFilePath)}_thumb.png";
            }

            string safeJobName = SanitizeFolderName(CurrentJob.JobName);
            string safeEquipName = SanitizeFolderName(equip.Name);
            string jobFolder = $"{CurrentJob.JobNumber}_{safeJobName}";
            string equipFolder = $"{CurrentJob.JobNumber}_{equip.EquipmentId}_{safeEquipName}";

            string ftpPath, targetLocalPath;

            if (part == null)
            {
                // Ekipman Resmi
                ftpPath = $"Attachments/Images/{jobFolder}/{equipFolder}/{thumbName}";
                targetLocalPath = Path.Combine(localImagesBase, jobFolder, equipFolder, thumbName);
            }
            else
            {
                // Parça Resmi
                string safePartName = SanitizeFolderName(part.Name);
                string partFolder = $"{CurrentJob.JobNumber}_{equip.EquipmentId}_{part.PartId}_{safePartName}";
                ftpPath = $"Attachments/Images/{jobFolder}/{equipFolder}/{partFolder}/{thumbName}";
                targetLocalPath = Path.Combine(localImagesBase, jobFolder, equipFolder, partFolder, thumbName);
            }

            // Dosya yerelde yoksa FTP'den indir
            if (!File.Exists(targetLocalPath))
            {
                string targetDir = Path.GetDirectoryName(targetLocalPath);
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                await _ftpHelper.DownloadFileAsync(ftpPath, targetLocalPath);
            }

            // Dosya varsa (veya yeni indiyse), modeldeki yolu güncelle
            if (File.Exists(targetLocalPath))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Dinamik tür olduğu için property'yi reflection veya cast ile set etmemiz gerekebilir
                    // Ancak dynamic olduğu için direkt atama genellikle çalışır.
                    attachment.ThumbnailPath = targetLocalPath;
                });
            }
        }


        // Arayüzün durumunu güncelleyen yardımcı metot
        private void UpdateApprovalStatus()
        {
            if (CurrentJob == null) return;
            IsPending = CurrentJob.MainApproval == ApprovalStatus.Pending;
            IsApproved = CurrentJob.MainApproval == ApprovalStatus.Approved;
            IsRejected = CurrentJob.MainApproval == ApprovalStatus.Rejected;
        }

        // --- YENİ ONAY KOMUTLARI ---

        [RelayCommand]
        async Task ApproveJob()
        {
            if (CurrentJob == null) return;

            // 1. Tehlikeli 'UpdateJobAsync' yerine yeni, güvenli metodu çağır
            await _jobService.UpdateJobApprovalAsync(CurrentJob.Id, ApprovalStatus.Approved);

            // 2. ViewModel'deki 'CurrentJob' nesnesini manuel olarak güncelle
            CurrentJob.MainApproval = ApprovalStatus.Approved;

            // 3. Arayüzü güncelle (butonları gizle, ekipmanları göster)
            UpdateApprovalStatus();
        }

        [RelayCommand]
        async Task RejectJob()
        {
            if (CurrentJob == null) return;

            // 1. Tehlikeli 'UpdateJobAsync' yerine yeni, güvenli metodu çağır
            await _jobService.UpdateJobApprovalAsync(CurrentJob.Id, ApprovalStatus.Rejected);

            // 2. ViewModel'deki 'CurrentJob' nesnesini manuel olarak güncelle
            CurrentJob.MainApproval = ApprovalStatus.Rejected;

            // 3. Arayüzü güncelle (butonları gizle, red mesajını göster)
            UpdateApprovalStatus();
        }


        // --- MEVCUT TÜM KOMUTLARINIZ (AddNewEquipment, AddNewPart, DeleteEquipment, vb.) ---
        // Bu metotların hepsi burada SİZİN KODUNUZDA olduğu gibi kalıyor.
        [RelayCommand]
        async Task AddNewEquipment()
        {
            if (CurrentJob == null) return;
            string newEquipName = await Shell.Current.DisplayPromptAsync(
                title: "Yeni Ekipman Ekle",
                message: $"'{CurrentJob.JobName}' işine eklenecek yeni ekipmanın adını girin:",
                placeholder: "Örn: DOZAJ POMPASI");

            if (string.IsNullOrWhiteSpace(newEquipName)) return;
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                var (nextId, nextCode) = await _equipmentService.GetNextEquipmentIdsAsync(CurrentJob);

                var newEquipment = new Equipment
                {
                    Name = newEquipName,
                    EquipmentId = nextId,
                    EquipmentCode = nextCode,
                    Parts = new ObservableCollection<EquipmentPart>()
                };

                var savedEquipment = await _equipmentService.AddEquipmentAsync(CurrentJob, newEquipment);

                if (savedEquipment != null)
                {
                    CurrentJob.Equipments.Add(savedEquipment);

                    // --- YENİ EKLENEN KISIM: EXCEL'İ OTOMATİK GÜNCELLE ---
                    await SaveExcelToDiskAsync();
                    // ----------------------------------------------------
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "Ekipman eklenemedi: " + ex.Message, "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }
        [RelayCommand]
        private async Task AddNewPart(Equipment parentEquipment)
        {
            if (parentEquipment == null) return;

            string newPartName = await Shell.Current.DisplayPromptAsync(
                title: "Yeni Parça Ekle",
                message: $"'{parentEquipment.Name}' altına eklenecek yeni parçanın adını girin:",
                placeholder: "Örn: YEDEK MOTOR");

            if (string.IsNullOrWhiteSpace(newPartName)) return;
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                var (nextPartId, nextPartCode) = await _partService.GetNextPartIdsAsync(parentEquipment);

                var newPart = new EquipmentPart
                {
                    Name = newPartName,
                    PartId = nextPartId,
                    PartCode = nextPartCode
                };

                var savedPart = await _partService.AddNewPartAsync(parentEquipment, newPart);

                if (savedPart != null)
                {
                    parentEquipment.Parts.Add(savedPart);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "Parça eklenemedi: " + ex.Message, "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }


        [RelayCommand]
        async Task DeleteEquipment(Equipment equipmentToDelete)
        {
            if (equipmentToDelete == null) return;

            // Kullanıcıdan onay al
            bool confirmed = await Shell.Current.DisplayAlert(
                "Ekipmanı Sil",
                $"'{equipmentToDelete.Name}' ({equipmentToDelete.EquipmentId}) ekipmanını silmek istediğinizden emin misiniz?\n\nBu ekipmana bağlı TÜM parçalar da kalıcı olarak silinecektir.",
                "Evet, Sil",
                "İptal");

            if (!confirmed) return;
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // 1. Servis ile veritabanından sil
                await _equipmentService.DeleteEquipmentAsync(equipmentToDelete.Id);

                // 2. Arayüzdeki listeden (ObservableCollection) kaldır
                CurrentJob.Equipments.Remove(equipmentToDelete);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "Ekipman silinirken bir hata oluştu: " + ex.Message, "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Sadece bir parçayı siler.
        /// </summary>
        [RelayCommand]
        async Task DeleteEquipmentPart(EquipmentPart partToDelete)
        {
            if (partToDelete == null) return;

            // Basit bir onay al
            bool confirmed = await Shell.Current.DisplayAlert(
                "Parçayı Sil",
                $"'{partToDelete.Name}' parçasını silmek istediğinizden emin misiniz?",
                "Evet, Sil",
                "İptal");

            if (!confirmed) return;
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // 1. Servis ile veritabanından sil
                await _partService.DeleteEquipmentPart(partToDelete.Id);

                // 2. Arayüzden kaldır (Parçanın ait olduğu ekipmanı bul)
                var parentEquipment = CurrentJob.Equipments
                    .FirstOrDefault(e => e.Parts.Contains(partToDelete));

                if (parentEquipment != null)
                {
                    parentEquipment.Parts.Remove(partToDelete);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "Parça silinirken bir hata oluştu: " + ex.Message, "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }
        /// <summary>
        /// Kullanıcıya dosya seçtirir ve 'AttachmentService' aracılığıyla ekler.
        /// </summary>
        [RelayCommand]
        async Task AddNewAttachment(Equipment parentEquipment)
        {
            if (parentEquipment == null || CurrentJob == null) return;

            try
            {
                // 1. Kullanıcıya dosya seçtir
                // Sadece PDF ve DWG dosyalarına izin ver (istediğiniz gibi genişletin)
                var fileTypes = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".pdf", ".dwg", ".jpg", ".png", ".txt" } },
                        { DevicePlatform.macOS, new[] { "pdf", "dwg", "jpg", "png", "txt" } }
                    });

                var pickOptions = new PickOptions
                {
                    PickerTitle = "Ekipman için dosya seçin",
                    FileTypes = fileTypes
                };

                FileResult fileResult = await FilePicker.Default.PickAsync(pickOptions);

                if (fileResult == null) return; // Kullanıcı iptal etti

                if (IsBusy) return;
                IsBusy = true;

                // 2. Servis aracılığıyla dosyayı kopyala ve veritabanına ekle
                var savedAttachment = await _attachmentService.AddAttachmentAsync(CurrentJob, parentEquipment, fileResult);

                // 3. Arayüzü güncelle
                if (savedAttachment != null)
                {
                    parentEquipment.Attachments.Add(savedAttachment);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "Dosya eklenemedi: " + ex.Message, "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }
        /// <summary>
        /// Seçilen dosyayı sistemin varsayılan uygulamasıyla açar.
        /// </summary>
        [RelayCommand]
        async Task OpenAttachment(EquipmentAttachment attachment)
        {
            if (attachment == null) return;
            await _attachmentService.OpenAttachmentAsync(attachment);
        }

        /// <summary>
        /// Seçilen dosyayı siler.
        /// </summary>
        [RelayCommand]
        async Task DeleteAttachment(EquipmentAttachment attachment)
        {
            if (attachment == null) return;

            bool confirmed = await Shell.Current.DisplayAlert("Dosyayı Sil", $"'{attachment.FileName}' dosyasını kalıcı olarak silmek istediğinizden emin misiniz?", "Evet, Sil", "İptal");
            if (!confirmed) return;

            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // 1. Servis ile sil
                await _attachmentService.DeleteAttachmentAsync(attachment);

                // 2. Arayüzden kaldır
                var parentEquipment = CurrentJob.Equipments
                    .FirstOrDefault(e => e.Attachments.Contains(attachment));

                if (parentEquipment != null)
                {
                    parentEquipment.Attachments.Remove(attachment);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Bir Parçaya yeni dosya ekler
        /// </summary>
        [RelayCommand]
        async Task AddNewPartAttachment(EquipmentPart parentPart)
        {
            if (parentPart == null || CurrentJob == null) return;

            // Bu parçanın bağlı olduğu Ekipmanı bul
            var parentEquipment = CurrentJob.Equipments.FirstOrDefault(e => e.Parts.Contains(parentPart));
            if (parentEquipment == null)
            {
                await Shell.Current.DisplayAlert("Hata", "Üst ekipman bulunamadı.", "Tamam");
                return;
            }

            try
            {
                var fileResult = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Parça için dosya seçin",
                    // İzin verilen dosya türlerini burada tanımlayabilirsiniz
                });

                if (fileResult == null) return; // Kullanıcı iptal etti

                if (IsBusy) return;
                IsBusy = true;

                // 2. Servis aracılığıyla dosyayı kopyala ve veritabanına ekle
                var savedAttachment = await _equipmentPartAttachmentService.AddAttachmentAsync(CurrentJob, parentEquipment, parentPart, fileResult);

                // 3. Arayüzü güncelle
                if (savedAttachment != null)
                {
                    parentPart.Attachments.Add(savedAttachment);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "Parça dosyası eklenemedi: " + ex.Message, "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Seçilen parça dosyasını açar
        /// </summary>
        [RelayCommand]
        async Task OpenPartAttachment(EquipmentPartAttachment attachment)
        {
            if (attachment == null) return;
            await _equipmentPartAttachmentService.OpenAttachmentAsync(attachment);
        }

        /// <summary>
        /// Seçilen parça dosyasını siler
        /// </summary>
        [RelayCommand]
        async Task DeletePartAttachment(EquipmentPartAttachment attachment)
        {
            if (attachment == null) return;

            bool confirmed = await Shell.Current.DisplayAlert("Dosyayı Sil", $"'{attachment.FileName}' dosyasını kalıcı olarak silmek istediğinizden emin misiniz?", "Evet, Sil", "İptal");
            if (!confirmed) return;

            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // 1. Servis ile sil
                await _equipmentPartAttachmentService.DeleteAttachmentAsync(attachment);

                // 2. Arayüzden kaldır
                foreach (var equip in CurrentJob.Equipments)
                {
                    var parentPart = equip.Parts.FirstOrDefault(p => p.Attachments.Contains(attachment));
                    if (parentPart != null)
                    {
                        parentPart.Attachments.Remove(attachment);
                        break;
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [ObservableProperty]
        bool isEditing = false;

        // --- GÜNCELLEME KOMUTU ---
        [RelayCommand]
        async Task UpdateJob()
        {
            if (CurrentJob == null) return;

            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // GÜNCELLEME: ID kontrolü ve Servis çağrısı
                // CurrentJob, UI'dan gelen güncel verileri tutar.
                await _jobService.UpdateJob(CurrentJob.Id, CurrentJob);

                await Shell.Current.DisplayAlert("Başarılı", "İş bilgileri güncellendi.", "Tamam");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Güncelleme sırasında hata oluştu: {ex.Message}", "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CheckAttachmentsIntegrityAsync()
        {
            //if (!IsAdminUser) return;
            // Dosya yolu kontrolü (Gerekirse)
            string basePath = Preferences.Get("attachment_path", string.Empty);
            if (string.IsNullOrWhiteSpace(basePath)) return;

            // 1. Ekipman Dosyaları Kontrolü
            foreach (var equipment in CurrentJob.Equipments)
            {
                for (int i = equipment.Attachments.Count - 1; i >= 0; i--)
                {
                    var attachment = equipment.Attachments.ElementAt(i);

                    // DÜZELTME: FileName ile yol uydurmak yerine, veritabanındaki kayıtlı tam yolu (FilePath) kullanıyoruz.
                    if (!File.Exists(attachment.FilePath))
                    {
                        // Dosya gerçekten yoksa kaydı sil
                        await _attachmentService.DeleteAttachmentRecordAsync(attachment.Id);
                        equipment.Attachments.RemoveAt(i);
                    }
                }

                // 2. Parça Dosyaları Kontrolü
                foreach (var part in equipment.Parts)
                {
                    for (int i = part.Attachments.Count - 1; i >= 0; i--)
                    {
                        var attachment = part.Attachments.ElementAt(i);

                        // DÜZELTME: Burada da FilePath kullanıyoruz.
                        if (!File.Exists(attachment.FilePath))
                        {
                            await _equipmentPartAttachmentService.DeletePartAttachmentRecordAsync(attachment.Id);
                            part.Attachments.RemoveAt(i);
                        }
                    }
                }
            }
        }

        [RelayCommand]
        async Task CreateExcelReport()
        {
            if (CurrentJob == null) return;
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // 1. Klasör Yolunu Bul
                string baseAttachmentPath = Preferences.Get("attachment_path", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase"));
                baseAttachmentPath = Path.Combine(baseAttachmentPath, "Attachments");

                // İsim temizleme (JobService'deki metodun aynısını burada kullanıyoruz veya helper yapabiliriz)
                string safeJobName = Regex.Replace(CurrentJob.JobName, @"[\\/:*?""<>| ]", "_");
                safeJobName = Regex.Replace(safeJobName, @"_+", "_").Trim('_');

                string jobFolder = $"{CurrentJob.JobNumber}_{safeJobName}";
                string directoryPath = Path.Combine(baseAttachmentPath, jobFolder);

                // Klasör yoksa oluştur
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // 2. Dosya Adını Oluştur (Örn: 3_Aşkale.xlsx)
                string fileName = $"{CurrentJob.JobNumber}_{safeJobName}.xlsx";
                string filePath = Path.Combine(directoryPath, fileName);

                // 3. Veriyi Hazırla
                // Sadece Excel'e basılacak basit bir liste oluşturuyoruz
                var excelData = CurrentJob.Equipments.Select(e => new
                {
                    EkipmanNo = e.EquipmentCode, // Örn: 3.1
                    EkipmanAdi = e.Name,         // Örn: Dozaj Pompası
                    ParcaSayisi = e.Parts.Count,
                    DosyaSayisi = e.Attachments.Count
                });

                // 4. Excel'i Kaydet (MiniExcel ile çok basit)
                await MiniExcel.SaveAsAsync(filePath, excelData);

                // 5. Kullanıcıya Bilgi Ver
                bool openFile = await Shell.Current.DisplayAlert("Başarılı",
                    $"Excel dosyası oluşturuldu:\n{fileName}\n\nKlasörde açmak ister misiniz?",
                    "Evet", "Hayır");

                if (openFile)
                {
                    // Dosyayı açmayı dene
                    await Launcher.Default.OpenAsync(new OpenFileRequest
                    {
                        File = new ReadOnlyFile(filePath)
                    });
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Excel oluşturulurken hata: {ex.Message}", "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }


        private async Task SaveExcelToDiskAsync()
        {
            if (CurrentJob == null) return;

            try
            {
                // 1. Veritabanından En Güncel ve Dolu Veriyi Çek (Includes ile)
                // UI'daki CurrentJob yerine, veritabanından taze kopya alıyoruz.
                var freshJobData = await _jobService.GetJobByIdAsync(CurrentJob.Id);

                if (freshJobData == null) return;

                // 2. Klasör Yollarını Hazırla
                string baseAttachmentPath = Preferences.Get("attachment_path", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase"));
                baseAttachmentPath = Path.Combine(baseAttachmentPath, "Attachments");

                string safeJobName = Regex.Replace(freshJobData.JobName, @"[\\/:*?""<>| ]", "_");
                safeJobName = Regex.Replace(safeJobName, @"_+", "_").Trim('_');

                string jobFolder = $"{freshJobData.JobNumber}_{safeJobName}";
                string directoryPath = Path.Combine(baseAttachmentPath, jobFolder);

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                string fileName = $"{freshJobData.JobNumber}_{safeJobName}.xlsx";
                string filePath = Path.Combine(directoryPath, fileName);

                // 3. Veriyi Hazırla (freshJobData kullanarak)
                var excelData = freshJobData.Equipments.Select(e => new
                {
                    EkipmanNo = e.EquipmentCode,
                    EkipmanAdi = e.Name,
                    // Null kontrolü yaparak sayıları alıyoruz
                    ParcaSayisi = e.Parts != null ? e.Parts.Count : 0,
                    DosyaSayisi = (e.Attachments?.Count ?? 0) + (e.Parts?.Sum(p => p.Attachments?.Count ?? 0) ?? 0), // Hem ekipman hem parça dosyalarını topla
                    EklemeTarihi = DateTime.Now.ToString("dd.MM.yyyy HH:mm")
                });

                // 4. Kaydet
                await MiniExcel.SaveAsAsync(filePath, excelData, overwriteFile: true);
                var jobForFtp = CurrentJob; // Thread güvenliği için kopyala
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Klasör yolunu tekrar hesapla (ViewModel içinde sanitize metodu yoksa kopyala veya helper kullan)
                        // Burada basitçe Regex kullanabilirsin:
                        string safeJobName = Regex.Replace(jobForFtp.JobName, @"[\\/:*?""<>| ]", "_");
                        safeJobName = Regex.Replace(safeJobName, @"_+", "_").Trim('_');
                        string jobFolder = $"{jobForFtp.JobNumber}_{safeJobName}";

                        // Hedef FTP klasörü: Attachments/IsKlasoru/
                        string ftpFolderPath = $"Attachments/{jobFolder}";

                        // Klasör olduğundan emin ol
                        await _ftpHelper.CreateDirectoryAsync("Attachments");
                        await _ftpHelper.CreateDirectoryAsync(ftpFolderPath);

                        // Yükle
                        await _ftpHelper.UploadFileAsync(filePath, ftpFolderPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Excel FTP Hatası: {ex.Message}");
                    }
                });
            }
            catch (IOException)
            {
                bool retry = await Shell.Current.DisplayAlert("Uyarı",
                    "Excel dosyası şu anda açık olduğu için güncellenemiyor.\n\nLütfen Excel'i kapatıp 'Tekrar Dene' butonuna basın.",
                    "Tekrar Dene", "İptal Et");

                if (retry) await SaveExcelToDiskAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Excel hatası: {ex.Message}", "Tamam");
            }
        }

        [RelayCommand]
        async Task OpenExcelFile()
        {
            if (CurrentJob == null) return;

            try
            {
                string basePath = Preferences.Get("attachment_path", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase"));
                basePath = Path.Combine(basePath, "Attachments");

                string safeJobName = SanitizeFolderName(CurrentJob.JobName);
                string jobFolder = $"{CurrentJob.JobNumber}_{safeJobName}";
                string fileName = $"{CurrentJob.JobNumber}_{safeJobName}.xlsx";
                string filePath = Path.Combine(basePath, jobFolder, fileName);

                // 1. Yerel Kontrol
                if (!File.Exists(filePath))
                {
                    // 2. Yoksa İndirmeyi Dene (Sormadan dene, Excel önemlidir)
                    if (IsBusy) return;
                    IsBusy = true;

                    try
                    {
                        string remotePath = $"Attachments/{jobFolder}/{fileName}";
                        await _ftpHelper.DownloadFileAsync(remotePath, filePath);
                    }
                    finally { IsBusy = false; }
                }

                // 3. Hala yoksa (ve Adminse) oluştur
                if (!File.Exists(filePath))
                {
                    //if (IsAdminUser) 
                    //{
                        await SaveExcelToDiskAsync();
                    //}
                    //else
                    //{
                    //    await Shell.Current.DisplayAlert("Bilgi", "Excel dosyası henüz oluşturulmamış.", "Tamam");
                    //    return;
                    //}
                }

                // 4. Aç
                await Launcher.Default.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(filePath) });
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Excel hatası: {ex.Message}", "Tamam");
            }
        }


        [RelayCommand]
        async Task ToggleEquipmentStatus(Equipment equipment)
        {
            if (equipment == null) return;

            string actionName = equipment.IsCancelled ? "AKTİF ETMEK" : "İPTAL ETMEK";
            string msg = equipment.IsCancelled ? "Ekipman tekrar aktif olacak." : "Ekipman iptal edildi olarak işaretlenecek.";

            bool confirmed = await Shell.Current.DisplayAlert("Ekipman Durumu",
                $"'{equipment.Name}' ekipmanını {actionName} istiyor musunuz?\n{msg}", "Evet", "Vazgeç");

            if (!confirmed) return;

            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // Yeni durum
                bool newStatus = !equipment.IsCancelled;

                // 1. Veritabanını Güncelle
                // (ServiceScope kullanmamıza gerek yok çünkü JobDetailsViewModel zaten transient ve her açılışta yenileniyor, 
                // ancak garanti olsun diye scope kullanabilir veya direkt servisi çağırabilirsiniz. 
                // Burada scope kullanmak en güvenlisidir.)

                // Eğer _serviceProvider injected değilse (ki constructor'da yoktu), direkt servisi kullanabiliriz 
                // ama en temizi UI'ı hemen güncellemektir.

                await _equipmentService.ToggleEquipmentStatusAsync(equipment.Id, newStatus);

                // 2. UI'ı Güncelle (Model Observable olduğu için renk OTOMATİK değişecek)
                equipment.IsCancelled = newStatus;
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        async Task TogglePartStatus(EquipmentPart part)
        {
            if (part == null) return;

            string actionName = part.IsCancelled ? "AKTİF ETMEK" : "İPTAL ETMEK";
            bool confirmed = await Shell.Current.DisplayAlert("Parça Durumu",
                $"'{part.Name}' parçasını {actionName} istiyor musunuz?", "Evet", "Vazgeç");

            if (!confirmed) return;

            if (IsBusy) return;
            IsBusy = true;

            try
            {
                bool newStatus = !part.IsCancelled;

                // Servisi çağır
                await _partService.TogglePartStatusAsync(part.Id, newStatus);

                // UI güncelle
                part.IsCancelled = newStatus;
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }


        [RelayCommand]
        async Task OpenFile(string dbFilePath) // DB'den gelen Relative Path (Attachments/...)
        {
            if (string.IsNullOrWhiteSpace(dbFilePath)) return;

            try
            {
                string fileName = Path.GetFileName(dbFilePath);
                string localFullPath = "";

                if (IsAdminUser)
                {
                    // ADMIN: Gerçek yerel yolu oluştur
                    // dbFilePath zaten "Attachments\Klasör\Dosya" formatında (veya /)
                    string basePath = Preferences.Get("attachment_path", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase"));

                    // Path.Combine platforma göre / veya \ düzeltir
                    localFullPath = Path.Combine(basePath, dbFilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                }
                else
                {
                    // USER: Cache klasörü
                    localFullPath = Path.Combine(FileSystem.CacheDirectory, fileName);
                }

                // 1. Dosya var mı?
                if (File.Exists(localFullPath))
                {
                    await Launcher.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(localFullPath) });
                    return;
                }

                // 2. Yoksa İndir
                bool answer = await Shell.Current.DisplayAlert("İndir", "Dosya cihazda yok. İndirilsin mi?", "Evet", "Hayır");
                if (!answer) return;

                if (IsBusy) return;
                IsBusy = true;

                try
                {
                    // dbFilePath zaten FTP yolu formatında (Attachments/...)
                    // Sadece ters slashları düze çevirelim garanti olsun
                    string ftpPath = dbFilePath.Replace("\\", "/");

                    await _ftpHelper.DownloadFileAsync(ftpPath, localFullPath);

                    if (File.Exists(localFullPath))
                    {
                        await Launcher.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(localFullPath) });
                    }
                    else
                    {
                        await Shell.Current.DisplayAlert("Hata", "İndirme başarısız.", "Tamam");
                    }
                }
                finally { IsBusy = false; }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", ex.Message, "Tamam");
            }
        }
    }
}