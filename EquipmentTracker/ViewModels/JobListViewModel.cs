// Dosya: ViewModels/JobListViewModel.cs
//using CommunityToolkit.Maui.Storage;
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
        //private readonly IFolderPicker _folderPicker;
        private List<JobModel> _allJobsMasterList = new();

        [ObservableProperty]
        ObservableCollection<JobModel> _jobs;

        [ObservableProperty]
        string _searchText;

        [ObservableProperty]
        string _attachmentPath;

        public JobListViewModel(IJobService jobService)
        {
            _jobService = jobService;
            Title = "Tüm İşler";
            //_folderPicker = folderPicker;
            Jobs = new ObservableCollection<JobModel>();
            _searchText = string.Empty;
            //LoadAttachmentPathCommand.Execute(null);
        }

        //[RelayCommand]
        //async Task SelectAttachmentPath(CancellationToken cancellationToken)
        //{
        //    try
        //    {
        //        var result = await _folderPicker.PickAsync(cancellationToken);
        //        if (result.IsSuccessful)
        //        {
        //            // Seçilen klasörün yolunu Entry'ye yaz
        //            AttachmentPath = result.Folder.Path;

        //            // Yolu otomatik olarak kaydetmek için mevcut komutu tetikle
        //            await SaveAttachmentPath();
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        await Shell.Current.DisplayAlert("Hata", $"Klasör seçilemedi: {ex.Message}", "Tamam");
        //    }
        //}

        ///// <summary>
        ///// Kayıtlı dosya yolunu Preferences'tan okur ve ekrandaki Entry'ye yazar.
        ///// </summary>
        //[RelayCommand]
        //void LoadAttachmentPath()
        //{
        //    // 'Belgelerim\TrackerDatabase'i varsayılan yol olarak ayarla
        //    string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase");

        //    // "attachment_path" anahtarıyla kayıtlı yolu getir, yoksa varsayılanı kullan
        //    AttachmentPath = Preferences.Get("attachment_path", defaultPath);
        //}


        ///// <summary>
        ///// Ekrana girilen yeni yolu Preferences'a kaydeder.
        ///// </summary>
        //[RelayCommand]
        //async Task SaveAttachmentPath()
        //{
        //    if (string.IsNullOrWhiteSpace(AttachmentPath))
        //    {
        //        await Shell.Current.DisplayAlert("Hata", "Dosya yolu boş olamaz.", "Tamam");
        //        return;
        //    }

        //    // Klasörü oluşturmayı dene (izin kontrolü için)
        //    try
        //    {
        //        if (!Directory.Exists(Path.Combine(AttachmentPath, "Attachments")))
        //        {
        //            Directory.CreateDirectory(Path.Combine(AttachmentPath, "Attachments"));
        //        }

        //        // Yolu kaydet
        //        Preferences.Set("attachment_path", AttachmentPath);
        //        await Shell.Current.DisplayAlert("Başarılı", "Yeni dosya yolu kaydedildi.", "Tamam");
        //    }
        //    catch (Exception ex)
        //    {
        //        // Genellikle 'C:\' gibi izin olmayan bir yere kaydetmeye çalışınca bu hata alınır.
        //        await Shell.Current.DisplayAlert("Hata", $"Yol kaydedilemedi. Geçerli bir klasör olduğundan emin olun.\n\nHata: {ex.Message}", "Tamam");
        //    }
        //}


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
                ).ToList(); // 2. Filtrelemeyi .ToList() ile hemen yap

            // 3. ÇÖZÜM: UI GÜNCELLEMESİNİ ANA THREAD'E ZORLA
            // Bu, "Collection must be modified on the UI thread" çökmesini engeller.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Jobs.Clear();
                foreach (var job in filteredJobs)
                {
                    Jobs.Add(job);
                }
            });
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