// Dosya: Views/DashboardPage.xaml.cs
using EquipmentTracker.ViewModels;

namespace EquipmentTracker.Views;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;

    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel; // viewModel'i sakla
    }

    // Sayfa her göründüðünde (Appearing) XAML'deki Behavior
    // _viewModel.LoadStatisticsCommand'ý tetikleyecek.
}