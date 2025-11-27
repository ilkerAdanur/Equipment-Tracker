using EquipmentTracker.ViewModels;

namespace EquipmentTracker.Views
{
    public partial class UserManagementPage : ContentPage
    {
        public UserManagementPage(UserManagementViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        // Sayfa her göründüðünde listeyi yenilemek için (Opsiyonel)
        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is UserManagementViewModel vm)
            {
                vm.LoadUsersCommand.Execute(null);
            }
        }
    }
}