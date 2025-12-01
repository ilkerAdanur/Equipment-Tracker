using EquipmentTracker.ViewModels;

namespace EquipmentTracker.Views;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _viewModel;

    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Sayfa açýlýnca kontrolü baþlat
        // Eðer internet yoksa kullanýcýyý bloklar
        await _viewModel.CheckInternetAndDbConnectionLoop();
    }
}