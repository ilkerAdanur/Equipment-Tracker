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
        private List<JobModel> _allJobsMasterList = new();

        [ObservableProperty]
        ObservableCollection<JobModel> _jobs;

        [ObservableProperty]
        string _searchText;

        public JobListViewModel(IJobService jobService)
        {
            _jobService = jobService;
            Title = "Tüm İşler";
            Jobs = new ObservableCollection<JobModel>();
            _searchText = string.Empty;
        }

        // Sayfa yüklendiğinde verileri çeker
        [RelayCommand]
        async Task LoadJobsAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                // 1. Veritabanından her zaman TAM listeyi çek
                _allJobsMasterList = await _jobService.GetAllJobsAsync();

                // 2. Arama çubuğuna göre filtreleyip ekrana bas
                FilterJobs();
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
        partial void OnSearchTextChanged(string value)
        {
            FilterJobs();
        }
        private void FilterJobs()
        {
            var filteredJobs = string.IsNullOrWhiteSpace(SearchText)
                ? _allJobsMasterList // Arama boşsa, tüm listeyi al
                : _allJobsMasterList.Where(j => // Arama doluysa, filtrele
                    j.JobName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    j.JobNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                );

            Jobs.Clear();
            foreach (var job in filteredJobs)
            {
                Jobs.Add(job);
            }
        }

        // Yeni İş Ekle sayfasına git
        [RelayCommand]
        async Task GoToAddNewJob()
        {
            await Shell.Current.GoToAsync(nameof(AddNewJobPage));
        }

        [RelayCommand]
        async Task DeleteJob(JobModel job)
        {
            if (job == null) return;

            // Kullanıcıya onaylat
            bool confirmed = await Shell.Current.DisplayAlert(
                "İşi Sil",
                $"'{job.JobName}' (İş No: {job.JobNumber}) işini silmek istediğinizden emin misiniz? Bu işe bağlı TÜM ekipman ve parçalar da silinecektir.",
                "Evet, Sil",
                "İptal");

            if (!confirmed) return;

            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // 1. Servis aracılığıyla veritabanından sil
                await _jobService.DeleteJobAsync(job.Id);

                // 2. Ekranda gösterilen listeden sil
                Jobs.Remove(job);

                // 3. Ana listeden (master list) de sil
                _allJobsMasterList.Remove(job);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "İş silinirken bir hata oluştu: " + ex.Message, "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
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