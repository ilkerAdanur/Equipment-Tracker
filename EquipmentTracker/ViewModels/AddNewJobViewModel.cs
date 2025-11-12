// Dosya: ViewModels/AddNewJobViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentTracker.Models;
using EquipmentTracker.Services.Job;
using System.Collections.ObjectModel;

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
        
        [ObservableProperty] string _creatorName;
        [ObservableProperty] string _creatorRole;
        [ObservableProperty] string _jobDescription;

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
                // --- YENİ KONTROL ---
                // Veritabanı daha önce hazırlanmadıysa, şimdi hazırla.
                if (!MauiProgram.IsDatabaseInitialized)
                {
                    await _jobService.InitializeDatabaseAsync();
                    MauiProgram.IsDatabaseInitialized = true; // Hazırlandı olarak işaretle
                }
                // --- KONTROL SONU ---

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
            if (string.IsNullOrWhiteSpace(JobName) || string.IsNullOrWhiteSpace(JobOwner) || string.IsNullOrWhiteSpace(CreatorName))
            {
                await Shell.Current.DisplayAlert("Hata", "İş Adı, İş Sahibi ve Ekleyen Adı boş olamaz.", "Tamam");
                return;
            }

            var newJob = new JobModel
            {
                JobNumber = this.JobNumber,
                JobName = this.JobName,
                JobOwner = this.JobOwner,
                Date = this.Date,

                // --- YENİ VERİLER ---
                CreatorName = this.CreatorName,
                CreatorRole = this.CreatorRole,
                JobDescription = this.JobDescription,
                // MainApproval "Pending" olarak varsayılan gelir (Model'in constructor'ından)

                Equipments = new ObservableCollection<Equipment>()
            };

            await _jobService.AddJobAsync(newJob);
            await Shell.Current.GoToAsync("//JobListPage");
        }

        // İptal edip geri dönmek için
        [RelayCommand]
        async Task Cancel()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}