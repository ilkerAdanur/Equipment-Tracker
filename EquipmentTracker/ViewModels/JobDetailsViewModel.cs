// Dosya: ViewModels/JobDetailsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentTracker.Models;
using EquipmentTracker.Services.Job;
using EquipmentTracker.Services.EquipmentPartService;
using System.Collections.ObjectModel;
using EquipmentTracker.Services.EquipmentService;

namespace EquipmentTracker.ViewModels
{
    [QueryProperty(nameof(JobId), "JobId")]
    public partial class JobDetailsViewModel : BaseViewModel
    {
        private readonly IJobService _jobService;
        private readonly IEquipmentService _equipmentService;
        private readonly IEquipmentPartService _partService;

        [ObservableProperty]
        JobModel _currentJob;

        [ObservableProperty]
        int _jobId;

        public JobDetailsViewModel(IJobService jobService,
                                 IEquipmentService equipmentService,
                                 IEquipmentPartService partService)
        {
            _jobService = jobService;
            _equipmentService = equipmentService;
            _partService = partService;
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

    }
}