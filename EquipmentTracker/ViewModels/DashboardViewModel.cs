// Dosya: ViewModels/DashboardViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentTracker.Models;
using EquipmentTracker.Services.StatisticsService;
using EquipmentTracker.Services.StatisticsServices; // Yeni servisimiz

namespace EquipmentTracker.ViewModels
{
    public partial class DashboardViewModel : BaseViewModel
    {
        private readonly IStatisticsService _statisticsService;

        [ObservableProperty]
        Statistics _stats;

        public DashboardViewModel(IStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
            Title = "Dashboard";
            Stats = new Statistics(); // Boş modelle başla
        }

        [RelayCommand]
        async Task LoadStatisticsAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
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