// Dosya: Views/AddNewJobPage.xaml.cs
using EquipmentTracker.ViewModels;

namespace EquipmentTracker.Views;

public partial class AddNewJobPage : ContentPage
{
    public AddNewJobPage(AddNewJobViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}