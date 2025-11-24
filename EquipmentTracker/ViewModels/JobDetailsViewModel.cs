// Dosya: ViewModels/JobDetailsViewModel.cs
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentTracker.Models;
using EquipmentTracker.Models.Enums; // YENİ ENUM KULLANIMI
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
                                 IEquipmentPartAttachmentService equipmentPartAttachmentService)
        {
            _jobService = jobService;
            _equipmentService = equipmentService;
            _partService = partService; // Sizin isimlendirmeniz
            _attachmentService = attachmentService;
            _equipmentPartAttachmentService = equipmentPartAttachmentService;
            Title = "İş Detayı";
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
                    await CheckAttachmentsIntegrityAsync();
                    //Title = CurrentJob.JobName;
                    JobNameDisplay = new CopyableTextViewModel(CurrentJob.JobName);
                    JobOwnerDisplay = new CopyableTextViewModel(CurrentJob.JobOwner);
                    JobDescriptionDisplay = new CopyableTextViewModel(CurrentJob.JobDescription);

                    // Onay durumuna göre arayüzü kontrol edecek bool'ları ayarla
                    UpdateApprovalStatus();

                }
                else
                {
                    Title = "Detay Bulunamadı";
                }
            }
            finally
            {
                IsBusy = false;
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

                await Shell.Current.DisplayAlert("Başarılı", "İş detayları güncellendi.", "Tamam");
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
                // Dosya yolunu tekrar hesapla
                string baseAttachmentPath = Preferences.Get("attachment_path", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase"));
                baseAttachmentPath = Path.Combine(baseAttachmentPath, "Attachments");

                string safeJobName = Regex.Replace(CurrentJob.JobName, @"[\\/:*?""<>| ]", "_");
                safeJobName = Regex.Replace(safeJobName, @"_+", "_").Trim('_');

                string jobFolder = $"{CurrentJob.JobNumber}_{safeJobName}";
                string fileName = $"{CurrentJob.JobNumber}_{safeJobName}.xlsx";
                string filePath = Path.Combine(baseAttachmentPath, jobFolder, fileName);

                // Dosya yoksa (belki hiç ekipman eklenmedi ama butona basıldı), şimdi oluştur
                if (!File.Exists(filePath))
                {
                    await SaveExcelToDiskAsync();
                }

                // Dosyayı Aç
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(filePath)
                });
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Excel açılırken hata: {ex.Message}", "Tamam");
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
        async Task OpenFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            try
            {
                // Dosya yolunu kontrol et
                if (!File.Exists(filePath))
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "Dosya bulunamadı veya erişilemiyor.", "Tamam");
                    return;
                }

                // Dosyayı varsayılan programla aç (AutoCAD, Viewer vb.)
                await Launcher.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(filePath)
                });
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", $"Dosya açılamadı: {ex.Message}", "Tamam");
            }
        }

    }
}