using EquipmentTracker.ViewModels;

namespace EquipmentTracker.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    // YENÝ: Sayfa göründüðünde çalýþýr
    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is SettingsViewModel vm)
        {
            // Giriþ durumunu kontrol et ve butonu güncelle
            vm.RefreshUserStatus();
        }
    }
}