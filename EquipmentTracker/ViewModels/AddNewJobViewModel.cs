// Dosya: ViewModels/AddNewJobViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentTracker.Models;
using EquipmentTracker.Services.Job;

namespace EquipmentTracker.ViewModels
{
    public partial class AddNewJobViewModel : BaseViewModel
    {
        private readonly IJobService _jobService;

        // Form alanları için özellikler
        [ObservableProperty] string _jobNumber;
        [ObservableProperty] string _jobName;
        [ObservableProperty] string _jobOwner;
        [ObservableProperty] DateTime _date = DateTime.Now; // Varsayılan tarih

        public AddNewJobViewModel(IJobService jobService)
        {
            _jobService = jobService;
            Title = "Yeni İş Ekle";
        }

        // Sayfa yüklendiğinde otomatik sonraki numarayı çeker
        [RelayCommand]
        async Task LoadNextJobNumber()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                JobNumber = await _jobService.GetNextJobNumberAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Kaydet butonuna basıldığında
        [RelayCommand]
        async Task SaveJob()
        {
            if (string.IsNullOrWhiteSpace(JobName) || string.IsNullOrWhiteSpace(JobOwner))
            {
                await Shell.Current.DisplayAlert("Hata", "İş Adı ve İş Sahibi boş olamaz.", "Tamam");
                return;
            }

            // Yeni Job nesnesini oluştur
            var newJob = new JobModel
            {
                JobNumber = this.JobNumber, // Otomatik gelen
                JobName = this.JobName,   // Formdan gelen
                JobOwner = this.JobOwner, // Formdan gelen
                Date = this.Date,         // Formdan gelen
                //Equipments = new List<Equipment>() // Başlangıçta boş ekipman listesi
            };

            await _jobService.AddJobAsync(newJob);

            // Başarıyla kaydettikten sonra bir önceki sayfaya (JobDetails) dön
            await Shell.Current.GoToAsync("..");
        }

        // İptal edip geri dönmek için
        [RelayCommand]
        async Task Cancel()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}