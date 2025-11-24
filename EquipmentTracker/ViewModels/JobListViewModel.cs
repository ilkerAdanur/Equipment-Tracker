// Dosya: ViewModels/JobListViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EquipmentTracker.Models;
using EquipmentTracker.Services.Job;
using EquipmentTracker.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Threading;

namespace EquipmentTracker.ViewModels
{
    public partial class JobListViewModel : BaseViewModel
    {
        private readonly IJobService _jobService;
        private List<JobModel> _allJobsMasterList = new();
        private CancellationTokenSource _searchCts;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        ObservableCollection<JobModel> _jobs;

        [ObservableProperty]
        string _searchText;

       

        [ObservableProperty]
        string _attachmentPath;

        public JobListViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            Title = "Tüm İşler";
            Jobs = new ObservableCollection<JobModel>();
            _searchText = string.Empty;

            // --- MESAJI DİNLE ---
            WeakReferenceMessenger.Default.Register<ConnectionMessage>(this, (r, m) =>
            {
                if (m.Value)
                {
                    LoadJobsCommand.Execute(null);
                }
                else
                {
                    // Bağlantı kesilince listeyi boşalt
                    Jobs.Clear();
                    _allJobsMasterList.Clear();
                }
            });
        }

        [RelayCommand]
        async Task LoadJobsAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                // Taze servis kullanımı
                using (var scope = _serviceProvider.CreateScope())
                {
                    var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();

                    _allJobsMasterList = await jobService.GetAllJobsAsync();
                    FilterJobs();
                }
            }
            catch (Exception ex)
            {
                // Hata durumunda listeyi temizle
                Jobs.Clear();
                await Shell.Current.DisplayAlert("Hata", "İş listesi yüklenemedi: " + ex.Message, "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }
        partial void OnSearchTextChanged(string value)
        {
            // Filtrelemeyi hemen çalıştırma, 300ms ertele
            DebouncedFilterJobs();
        }

        /// <summary>
        /// Arama işlemini 300ms erteleyerek klavye odağının kaybolmasını engeller.
        /// </summary>
        private async void DebouncedFilterJobs()
        {
            try
            {
                // Önceki gecikmeyi iptal et (kullanıcı hala yazıyor)
                _searchCts?.Cancel();
                _searchCts = new CancellationTokenSource();

                // 300ms bekle
                await Task.Delay(300, _searchCts.Token);

                // Bekleme süresi dolduysa ve iptal edilmediyse, filtrelemeyi çalıştır
                FilterJobs();
            }
            catch (OperationCanceledException)
            {
                // Kullanıcı yazmaya devam ettiği için iptal edildi, normal bir durum.
            }
        }

        /// <summary>
        /// Listeyi "Jobs.Clear()" kullanmadan, akıllıca güncelleyerek
        /// SearchBar odağının (focus) kaybolmasını engeller.
        /// </summary>
        private void FilterJobs()
        {
            var filteredJobs = string.IsNullOrWhiteSpace(SearchText)
                ? _allJobsMasterList
                : _allJobsMasterList.Where(j =>
                    j.JobName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    j.JobNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                ).ToList();

            // FİLTRELENEN LİSTEYİ DE SIRALA (Numara sırasını korumak için)
            filteredJobs = filteredJobs
                .OrderBy(j => j.JobNumber.Length)
                .ThenBy(j => j.JobNumber)
                .ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                // ... (Silme döngüsü aynı kalacak)
                for (int i = Jobs.Count - 1; i >= 0; i--)
                {
                    var job = Jobs[i];
                    if (!filteredJobs.Contains(job))
                    {
                        Jobs.RemoveAt(i);
                    }
                }

                // ... (Ekleme/Taşıma döngüsü aynı kalacak)
                for (int i = 0; i < filteredJobs.Count; i++)
                {
                    var job = filteredJobs[i];
                    if (Jobs.Contains(job))
                    {
                        int currentIndex = Jobs.IndexOf(job);
                        if (currentIndex != i) Jobs.Move(currentIndex, i);
                    }
                    else
                    {
                        Jobs.Insert(i, job);
                    }
                }
            });
        }


        [RelayCommand]
        async Task ToggleJobStatus(JobModel job)
        {
            if (job == null) return;

            // Mesajları hazırla
            string actionName = job.IsCancelled ? "AKTİF ETMEK" : "İPTAL ETMEK";
            string statusMessage = job.IsCancelled ? "İş tekrar aktif hale gelecektir." : "İş iptal edildi olarak işaretlenecektir.";

            bool confirmed = await Shell.Current.DisplayAlert(
                "İş Durumu",
                $"'{job.JobName}' işini {actionName} istediğinizden emin misiniz?\n\n{statusMessage}",
                "Evet",
                "Vazgeç");

            if (!confirmed) return;

            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // Yeni durumu hesapla (Mevcut durumun tersi)
                bool newStatus = !job.IsCancelled;

                using (var scope = _serviceProvider.CreateScope())
                {
                    var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();

                    // YENİ METODU ÇAĞIRIYORUZ
                    // Klasör kontrollerine takılmadan sadece durumu günceller
                    await jobService.ToggleJobStatusAsync(job.Id, newStatus);
                }

                // UI tarafındaki nesneyi de güncelle (Listeyi komple yenilemeden renk değişimi için)
                job.IsCancelled = newStatus;

                // İsterseniz listeyi veritabanından tazelemek için:
                // await LoadJobsAsync(); 
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "Durum değiştirilirken hata oluştu: " + ex.Message, "Tamam");
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

        [RelayCommand]
        async Task DeleteJob(JobModel job)
        {
            if (job == null) return;

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
                using (var scope = _serviceProvider.CreateScope())
                {
                    var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
                    await jobService.DeleteJobAsync(job.Id);
                }

                Jobs.Remove(job);
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
            await Shell.Current.GoToAsync($"{nameof(JobDetailsPage)}?JobId={job.Id}", true);
        }
    }
}