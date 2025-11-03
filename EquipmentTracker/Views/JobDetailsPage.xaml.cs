// Dosya: Views/JobDetailsPage.xaml.cs
using EquipmentTracker.ViewModels; 

namespace EquipmentTracker.Views;

public partial class JobDetailsPage : ContentPage
{

    public JobDetailsPage(JobDetailsViewModel viewModel)
    {
        InitializeComponent();


        BindingContext = viewModel;
    }
}