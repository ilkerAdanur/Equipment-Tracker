// Dosya: ViewModels/JobDetailsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentTracker.Models;
using EquipmentTracker.Services.Job;

namespace EquipmentTracker.ViewModels
{
    // Bu 'QueryProperty' Shell'den 'jobId' parametresini almamızı sağlar
    [QueryProperty(nameof(JobId), "JobId")]
    public partial class JobDetailsViewModel : BaseViewModel
    {
        private readonly IJobService _jobService;

        // "Önceki/Sonraki" mantığı kaldırıldı
        [ObservableProperty]
        JobModel _currentJob;

        // Gelen 'jobId' parametresini tutar
        [ObservableProperty]
        int _jobId;

        public JobDetailsViewModel(IJobService jobService)
        {
            _jobService = jobService;
            Title = "İş Detayı";
        }

        // JobId set edildiğinde (sayfa açıldığında) çalışır
        partial void OnJobIdChanged(int value)
        {
            // Gelen Id ile sadece 1 işi yükle
            LoadJobDetailsCommand.Execute(value);
        }

        [RelayCommand]
        async Task LoadJobDetailsAsync(int jobId)
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                CurrentJob = await _jobService.GetJobByIdAsync(jobId);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // "Parça Ekle" komutu (Bu zaten vardı, aynı kalıyor)
        [RelayCommand]
        private async Task AddNewPart(Equipment parentEquipment)
        {
            if (parentEquipment == null) return;

            // 1. Kullanıcıdan yeni parçanın adını iste (Popup)
            string newPartName = await Shell.Current.DisplayPromptAsync(
                title: "Yeni Parça Ekle",
                message: $"'{parentEquipment.Name}' altına eklenecek yeni parçanın adını girin:",
                placeholder: "Örn: YEDEK MOTOR");

            // Kullanıcı iptal'e basmadıysa veya boş geçmediyse
            if (!string.IsNullOrWhiteSpace(newPartName))
            {
                if (IsBusy) return;
                IsBusy = true;

                try
                {
                    // 2. Yeni Parça nesnesini oluştur
                    // Otomatik PartId ve PartCode hesaplama
                    int nextPartId = parentEquipment.Parts.Count + 1;
                    string nextPartCode = $"{parentEquipment.EquipmentCode}.{nextPartId}";

                    var newPart = new EquipmentPart
                    {
                        Name = newPartName,
                        PartId = nextPartId.ToString(),
                        PartCode = nextPartCode
                    };

                    // 3. Servis katmanını kullanarak veritabanına kaydet
                    var savedPart = await _jobService.AddNewPartAsync(parentEquipment, newPart);

                    // 4. Veritabanından dönen (Id'si olan) parçayı
                    //    ViewModel'deki listeye ekle.
                    //    Liste 'ObservableCollection' olduğu için UI anında güncellenecek!
                    if (savedPart != null)
                    {
                        parentEquipment.Parts.Add(savedPart);
                    }
                }
                catch (Exception ex)
                {
                    await Shell.Current.DisplayAlert("Hata", "Parça eklenirken bir sorun oluştu.", "Tamam");
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }
    }
}