// Dosya: ViewModels/JobListViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentTracker.Models;
using EquipmentTracker.Services.Job;
using EquipmentTracker.Views;
using System.Collections.ObjectModel;

namespace EquipmentTracker.ViewModels
{
    public partial class JobListViewModel : BaseViewModel
    {
        private readonly IJobService _jobService;

        [ObservableProperty]
        ObservableCollection<JobModel> _jobs;

        public JobListViewModel(IJobService jobService)
        {
            _jobService = jobService;
            Title = "Tüm İşler";
            Jobs = new ObservableCollection<JobModel>();
        }

        // Sayfa yüklendiğinde verileri çeker
        [RelayCommand]
        async Task LoadJobsAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                Jobs.Clear();
                var jobList = await _jobService.GetAllJobsAsync();
                foreach (var job in jobList)
                {
                    Jobs.Add(job);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "İş listesi yüklenemedi", "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Yeni İş Ekle sayfasına git
        [RelayCommand]
        async Task GoToAddNewJob()
        {
            await Shell.Current.GoToAsync(nameof(AddNewJobPage));
        }

        // Detay sayfasına git (Parametre olarak JobId gönderir)
        [RelayCommand]
        async Task GoToDetails(JobModel job)
        {
            if (job == null) return;

            // JobDetailsPage'e 'jobId' parametresini göndererek git
            await Shell.Current.GoToAsync(nameof(JobDetailsPage), true, new Dictionary<string, object>
            {
                { "JobId", job.Id }
            });
        }
    }
}