// Dosya: Views/JobListPage.xaml.cs
using EquipmentTracker.ViewModels;

namespace EquipmentTracker.Views;

public partial class JobListPage : ContentPage
{
    private readonly JobListViewModel _viewModel;
    public JobListPage(JobListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    // Bu sayfa (tekrar) göründüðünde çalýþýr
    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Verileri yeniden yükle (yeni eklenen iþleri görmek için)
        _viewModel.LoadJobsCommand.Execute(null);
    }
}