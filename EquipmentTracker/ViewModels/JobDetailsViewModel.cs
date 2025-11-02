// Dosya: ViewModels/JobDetailsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentTracker.Models;
using EquipmentTracker.Services.Job;
using EquipmentTracker.Services.EquipmentService; // YENİ SERVİSİ KULLAN
using System.Collections.ObjectModel; // ObservableCollection için

namespace EquipmentTracker.ViewModels
{
    // Sayfaya 'JobId' parametresi alabilmek için
    [QueryProperty(nameof(JobId), "JobId")]
    public partial class JobDetailsViewModel : BaseViewModel
    {
        private readonly IJobService _jobService;
        private readonly IEquipmentService _equipmentService;

        [ObservableProperty]
        JobModel _currentJob;

        [ObservableProperty]
        int _jobId;

        // Hem IJobService hem IEquipmentService enjekte edilir
        public JobDetailsViewModel(IJobService jobService, IEquipmentService equipmentService)
        {
            _jobService = jobService;
            _equipmentService = equipmentService;
            Title = "İş Detayı";
        }

        // 'JobId' parametresi geldiğinde bu metot tetiklenir
        partial void OnJobIdChanged(int value)
        {
            // Gelen Id ile sadece 1 işi yükle
            LoadJobDetailsCommand.Execute(value);
        }

        // Sadece o İŞ'in detaylarını yükler
        [RelayCommand]
        async Task LoadJobDetailsAsync(int jobId)
        {
            if (jobId == 0) return;
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // JobService'ten SADECE o işin detaylarını istiyoruz
                // (Bu metodu JobService'e eklememiz gerekebilir, eğer yoksa)
                CurrentJob = await _jobService.GetJobByIdAsync(jobId);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "İş detayı yüklenemedi.", "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Mevcut İşe yeni bir EKİPMAN (örn: DOZAJ POMPASI) ekler.
        /// (İstediğiniz özellik bu)
        /// </summary>
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
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Bir Ekipmana yeni bir PARÇA (örn: TANK GÖVDESİ) ekler.
        /// (Bu, içteki (+) butonu içindir)
        /// </summary>
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
                var (nextPartId, nextPartCode) = await _equipmentService.GetNextPartIdsAsync(parentEquipment);
                var newPart = new EquipmentPart
                {
                    Name = newPartName,
                    PartId = nextPartId,
                    PartCode = nextPartCode
                };

                var savedPart = await _equipmentService.AddNewPartAsync(parentEquipment, newPart);

                if (savedPart != null)
                {
                    parentEquipment.Parts.Add(savedPart);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}