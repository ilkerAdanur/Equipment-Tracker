// Dosya: ViewModels/JobDetailsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentTracker.Models;
using EquipmentTracker.Services.Job;
using EquipmentTracker.Services.EquipmentPartService;
using System.Collections.ObjectModel;
using EquipmentTracker.Services.EquipmentService;
using EquipmentTracker.Services.AttachmentServices;

namespace EquipmentTracker.ViewModels
{
    [QueryProperty(nameof(JobId), "JobId")]
    public partial class JobDetailsViewModel : BaseViewModel
    {
        private readonly IJobService _jobService;
        private readonly IEquipmentService _equipmentService;
        private readonly IEquipmentPartService _partService;
        private readonly IAttachmentService _attachmentService;

        [ObservableProperty]
        JobModel _currentJob;

        [ObservableProperty]
        int _jobId;

        public JobDetailsViewModel(IJobService jobService,
                                 IEquipmentService equipmentService,
                                 IEquipmentPartService partService,
                                 IAttachmentService attachmentService)
        {
            _jobService = jobService;
            _equipmentService = equipmentService;
            _partService = partService;
            _attachmentService = attachmentService;
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
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "İş detayı yüklenemedi: " + ex.Message, "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }


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


    }
}