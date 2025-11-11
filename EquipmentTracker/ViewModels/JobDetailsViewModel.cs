// Dosya: ViewModels/JobDetailsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentTracker.Models;
using EquipmentTracker.Models.Enums; // YENİ ENUM KULLANIMI
using EquipmentTracker.Services.AttachmentServices;
using EquipmentTracker.Services.EquipmentPartAttachmentServices;
using EquipmentTracker.Services.EquipmentPartService;
using EquipmentTracker.Services.EquipmentService;
using EquipmentTracker.Services.Job;
using System.Collections.ObjectModel;
using System.Diagnostics; // Hata ayıklama için

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

        partial void OnJobIdChanged(int value)
        {
            LoadJobDetailsCommand.Execute(value);
        }

        [RelayCommand]
        async Task LoadJobDetailsAsync(int jobId)
        {
            if (jobId == 0) return;
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                CurrentJob = await _jobService.GetJobByIdAsync(jobId);

                if (CurrentJob != null)
                {
                    Title = CurrentJob.JobName;
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
    }
}