// Dosya: ViewModels/DashboardViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentTracker.Models;
using EquipmentTracker.Services.Job; // <-- YENİ EKLEYİN
using EquipmentTracker.Services.StatisticsService;
using EquipmentTracker.Services.StatisticsServices;

namespace EquipmentTracker.ViewModels
{
    public partial class DashboardViewModel : BaseViewModel
    {
        private readonly IStatisticsService _statisticsService;
        private readonly IJobService _jobService; // <-- YENİ EKLEYİN

        [ObservableProperty]
        Statistics _stats;

        // Constructor'ı güncelleyin
        public DashboardViewModel(IStatisticsService statisticsService, IJobService jobService)
        {
            _statisticsService = statisticsService;
            _jobService = jobService; // <-- YENİ EKLEYİN
            Title = "Dashboard";
            Stats = new Statistics();
        }

        [RelayCommand]
        async Task LoadStatisticsAsync()
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

                // Servisten hesaplanmış verileri çek
                Stats = await _statisticsService.GetDashboardStatisticsAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "İstatistikler yüklenemedi: " + ex.Message, "Tamam");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}