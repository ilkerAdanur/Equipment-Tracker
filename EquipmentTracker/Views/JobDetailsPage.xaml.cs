// Dosya: Views/JobDetailsPage.xaml.cs
using EquipmentTracker.ViewModels; // ViewModel'imizin olduğu yer

namespace EquipmentTracker.Views;

public partial class JobDetailsPage : ContentPage
{
    // 1. MauiProgram.cs'de kaydettiğimiz ViewModel'i
    //    'constructor injection' ile alıyoruz.
    public JobDetailsPage(JobDetailsViewModel viewModel)
    {
        InitializeComponent();

        // 2. Bu sayfanın tüm veri bağlamı (BindingContext)
        //    artık 'viewModel' nesnesidir.
        BindingContext = viewModel;
    }
}