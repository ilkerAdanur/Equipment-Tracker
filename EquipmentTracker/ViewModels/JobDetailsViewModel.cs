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
        private static readonly SemaphoreSlim _excelLock = new SemaphoreSlim(1, 1);
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
        async Task CopyToClipboard(object parameter) // Parametreyi 'string' yerine 'object' yaptık
        {
            if (parameter == null) return;

            string textToCopy = "";

            // 1. Durum: Eğer tıklanan bir Parça (EquipmentPart) ise (Y1 veya 5.1.1'e tıklandıysa)
            if (parameter is EquipmentPart part)
            {
                // İstenilen format: 5.1.1.Y1
                textToCopy = $"{part.PartCode}.{part.Name}";
            }
            // 2. Durum: Eğer tıklanan düz bir yazı ise (İş No, Dosya Yolu vb.)
            else if (parameter is string text)
            {
                textToCopy = text;

                // Dosya yolları için olan mevcut temizleme mantığınız:
                if (textToCopy.Contains("/") || textToCopy.Contains("\\"))
                {
                    string normalized = textToCopy.Replace("\\", "/");
                    if (normalized.StartsWith("Attachments/"))
                    {
                        var parts = normalized.Split('/');
                        if (parts.Length >= 4)
                        {
                            string lastFolder = parts[parts.Length - 2];
                            string fileName = parts[parts.Length - 1];
                            textToCopy = $"{lastFolder}_{fileName}";
                        }
                    }
                }
            }

            // Eğer kopyalanacak metin oluştuysa panoya kopyala
            if (!string.IsNullOrWhiteSpace(textToCopy))
            {
                await Clipboard.Default.SetTextAsync(textToCopy);

                if (Application.Current?.MainPage != null)
                    await Application.Current.MainPage.DisplayAlert("Kopyalandı", $"'{textToCopy}' panoya kopyalandı.", "Tamam");
            }
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

                    _ = Task.Run(DownloadThumbnailsForCurrentJob);

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
            // Önce dosyayı oluştur/güncelle
            await SaveExcelToDiskAsync();

            if (CurrentJob == null) return;
            string safeJobName = SanitizeFolderName(CurrentJob.JobName);
            string jobFolder = $"{CurrentJob.JobNumber}_{safeJobName}";
            string fileName = $"{CurrentJob.JobNumber}_{safeJobName}.xlsx";

            string basePath = Preferences.Get("attachment_path", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase"));
            string filePath = Path.Combine(basePath, "Attachments", jobFolder, fileName);

            if (File.Exists(filePath))
            {
                bool open = await Shell.Current.DisplayAlert("Rapor Hazır",
                    "Excel dosyası oluşturuldu.\nAçmak ister misiniz?",
                    "Evet", "Hayır");

                if (open)
                {
                    await Launcher.Default.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(filePath) });
                }
            }
        }

        // 2. METOT: Ana Kayıt Mantığı (DÜZELTİLDİ: Ekranda görünen veriyi kullanır)
        // 2. METOT: Ana Kayıt Mantığı (Dosya Açıksa Kopyasını Oluşturur)
        private async Task SaveExcelToDiskAsync()
        {
            if (CurrentJob == null) return;

            // Dosya çakışmalarını önlemek için bekle
            await _excelLock.WaitAsync();

            try
            {
                // Veriyi doğrudan ekrandan al (Hız ve güncellik için)
                var jobData = CurrentJob;

                // --- KLASÖR YOLLARI ---
                string baseAttachmentPath = Preferences.Get("attachment_path", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase"));
                baseAttachmentPath = Path.Combine(baseAttachmentPath, "Attachments");

                string safeJobName = SanitizeFolderName(jobData.JobName);
                string jobFolder = $"{jobData.JobNumber}_{safeJobName}";
                string directoryPath = Path.Combine(baseAttachmentPath, jobFolder);

                if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

                string baseFileName = $"{jobData.JobNumber}_{safeJobName}";
                string targetFilePath = Path.Combine(directoryPath, $"{baseFileName}.xlsx");

                // --- LİSTE OLUŞTURMA (TEK SÜTUN MANTIĞI) ---
                var excelData = new List<dynamic>();

                if (jobData.Equipments != null)
                {
                    foreach (var eq in jobData.Equipments)
                    {
                        // 1. Önce Ekipman Satırını Ekle (Örn: 4.1.D1)
                        // Kod ve Adı birleştiriyoruz.
                        string ekipmanTanim = $"{eq.EquipmentCode}.{eq.Name}";

                        excelData.Add(new
                        {
                            Ad = ekipmanTanim, // Tek sütun: "4.1.D1"
                            Dosya = eq.Attachments?.Count ?? 0,
                            Tarih = DateTime.Now.ToString("dd.MM.yyyy HH:mm")
                        });

                        // 2. Varsa Parçaları Altına Ekle (Örn: 4.1.1.Y1)
                        if (eq.Parts != null && eq.Parts.Count > 0)
                        {
                            foreach (var part in eq.Parts)
                            {
                                // Parça verisi (Girintili gözükmesi için başına boşluk veya simge koyabiliriz)
                                // İsteğiniz: 4.1.1.Y1
                                string parcaTanim = $"   ↳ {part.PartCode}.{part.Name}";

                                excelData.Add(new
                                {
                                    Ad = parcaTanim, // Tek sütun: "   ↳ 4.1.1.Y1"
                                    Dosya = part.Attachments?.Count ?? 0,
                                    Tarih = "" // Parça için tarih tekrarına gerek yoksa boş bırakılabilir veya doldurulabilir
                                });
                            }
                        }
                    }
                }

                // --- KAYDETME İŞLEMİ (LOCK KONTROLÜ İLE) ---
                string finalSavedPath = targetFilePath;
                bool savedAsCopy = false;

                try
                {
                    // Asıl dosyaya yazmayı dene
                    await MiniExcel.SaveAsAsync(targetFilePath, excelData, overwriteFile: true);
                }
                catch (IOException)
                {
                    // DOSYA AÇIKSA: Kopya oluştur (İşlem durmasın)
                    savedAsCopy = true;
                    string timestamp = DateTime.Now.ToString("HHmmss");
                    string copyFileName = $"{baseFileName}_Kopya_{timestamp}.xlsx";
                    finalSavedPath = Path.Combine(directoryPath, copyFileName);

                    await MiniExcel.SaveAsAsync(finalSavedPath, excelData, overwriteFile: true);
                }

                // --- FTP SENKRONİZASYONU ---
                var jobForFtp = CurrentJob;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        string sJobName = SanitizeFolderName(jobForFtp.JobName);
                        string jFolder = $"{jobForFtp.JobNumber}_{sJobName}";
                        string ftpFolder = $"Attachments/{jFolder}";

                        await _ftpHelper.CreateDirectoryAsync("Attachments");
                        await _ftpHelper.CreateDirectoryAsync(ftpFolder);
                        // Hangi dosya oluşturulduysa onu yükle (Kopya veya Orijinal)
                        await _ftpHelper.UploadFileAsync(finalSavedPath, ftpFolder);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Excel FTP Hatası: {ex.Message}");
                    }
                });

                // Kullanıcıya Bilgi (Sadece kopya oluşturmak zorunda kaldıysa)
                if (savedAsCopy)
                {
                    await Shell.Current.DisplayAlert("Bilgi",
                        $"Asıl Excel dosyası açık olduğu için veriler '{Path.GetFileName(finalSavedPath)}' adıyla yeni bir dosyaya kaydedildi.",
                        "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Excel hatası: {ex.Message}", "Tamam");
            }
            finally
            {
                // Kilidi kaldır
                _excelLock.Release();
            }
        }
        // 3. METOT: Dosya Açma
        [RelayCommand]
        async Task OpenExcelFile()
        {
            if (CurrentJob == null) return;
            try
            {
                string basePath = Preferences.Get("attachment_path", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase"));
                string safeJobName = SanitizeFolderName(CurrentJob.JobName);
                string jobFolder = $"{CurrentJob.JobNumber}_{safeJobName}";
                string fileName = $"{CurrentJob.JobNumber}_{safeJobName}.xlsx";
                string localFilePath = Path.Combine(basePath, "Attachments", jobFolder, fileName);

                // Dosya yoksa oluştur
                if (!File.Exists(localFilePath))
                {
                    await SaveExcelToDiskAsync();
                }

                // Aç
                if (File.Exists(localFilePath))
                {
                    await Launcher.Default.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(localFilePath) });
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", ex.Message, "Tamam");
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




        // Dosya Açma Komutu (Onay sorusu eklendi)
        [RelayCommand]
        async Task OpenFile(string dbFilePath)
        {
            if (string.IsNullOrWhiteSpace(dbFilePath)) return;

            // --- YENİ EKLENEN ONAY KUTUSU ---
            bool answer = await Shell.Current.DisplayAlert("Dosya Aç",
                "Bu dosyayı sistem varsayılan uygulamasıyla açmak istiyor musunuz?",
                "Evet", "Hayır");

            if (!answer) return; // Hayır derse çık
            // ---------------------------------

            try
            {
                string fileName = Path.GetFileName(dbFilePath);
                string localFullPath;

                if (IsAdminUser)
                {
                    string basePath = Preferences.Get("attachment_path", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase"));
                    localFullPath = Path.Combine(basePath, dbFilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                }
                else
                {
                    localFullPath = Path.Combine(FileSystem.CacheDirectory, fileName);
                }

                if (File.Exists(localFullPath))
                {
                    await Launcher.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(localFullPath) });
                    return;
                }

                // Dosya yoksa indirme onayı (Bu zaten vardı, kalsın)
                bool downloadConfirm = await Shell.Current.DisplayAlert("İndir", "Dosya yerelde bulunamadı. İndirilsin mi?", "Evet", "Hayır");
                if (!downloadConfirm) return;

                if (IsBusy) return;
                IsBusy = true;

                try
                {
                    string ftpPath = dbFilePath.Replace("\\", "/");
                    await _ftpHelper.DownloadFileAsync(ftpPath, localFullPath);

                    if (File.Exists(localFullPath))
                        await Launcher.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(localFullPath) });
                    else
                        await Shell.Current.DisplayAlert("Hata", "İndirme başarısız.", "Tamam");
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