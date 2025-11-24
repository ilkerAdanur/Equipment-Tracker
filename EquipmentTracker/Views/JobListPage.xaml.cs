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

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel?.LoadJobsCommand.CanExecute(null) == true)
        {
            _viewModel.LoadJobsCommand.Execute(null);
        }
    }
}