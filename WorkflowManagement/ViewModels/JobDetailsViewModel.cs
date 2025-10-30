// Dosya Adı: ViewModels/JobDetailsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input; // RelayCommand için
using System.Collections.ObjectModel; // ObservableCollection için
using WorkflowManagement.Models.Job;
using WorkflowManagement.Models.ApprovalStatus;
using WorkflowManagement.Services.Job;

namespace WorkflowManagement.ViewModels
{
    public partial class JobDetailsViewModel : BaseViewModel
    {
        private readonly IJobService _jobService;
        private List<ResultJobModel> _allJobs; 


        [ObservableProperty]
        private ResultJobModel _currentJob;

        [ObservableProperty]
        private int _currentJobIndex;

        public string PageIndicator => _allJobs != null && _allJobs.Any()
            ? $"{_currentJobIndex + 1} / {_allJobs.Count}"
            : "0 / 0";

        public JobDetailsViewModel(IJobService jobService)
        {
            _jobService = jobService;
            _allJobs = new List<ResultJobModel>();
            Title = "İş Detayları";

            LoadDataCommand.Execute(null);
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                _allJobs = await _jobService.GetAllJobsAsync();

                if (_allJobs != null && _allJobs.Any())
                {
                    CurrentJobIndex = 0;
                    CurrentJob = _allJobs[CurrentJobIndex];

                    OnPropertyChanged(nameof(PageIndicator));
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "İşler yüklenemedi.", "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void NextJob()
        {
            if (_currentJobIndex < _allJobs.Count - 1)
            {
                CurrentJobIndex++;
                CurrentJob = _allJobs[CurrentJobIndex];
                OnPropertyChanged(nameof(PageIndicator));
            }
        }

        [RelayCommand]
        private void PreviousJob()
        {
            if (_currentJobIndex > 0)
            {
                CurrentJobIndex--;
                CurrentJob = _allJobs[CurrentJobIndex];
                OnPropertyChanged(nameof(PageIndicator));
            }
        }

        [RelayCommand]
        private async Task ApproveClientAsync()
        {
            if (CurrentJob == null) return;
            CurrentJob.ClientApproval = ResultApprovalStatusModel.Approved;
            await _jobService.UpdateJobAsync(CurrentJob);
            OnPropertyChanged(nameof(CurrentJob));
        }

        [RelayCommand]
        private async Task RejectClientAsync()
        {
            if (CurrentJob == null) return;
            CurrentJob.ClientApproval = ResultApprovalStatusModel.Rejected;
            await _jobService.UpdateJobAsync(CurrentJob);
            OnPropertyChanged(nameof(CurrentJob));
        }

        // TODO: Malzeme ve Kargo onay/red komutları buraya eklenecek
    }
}