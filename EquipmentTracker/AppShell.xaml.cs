namespace EquipmentTracker
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Admin Kontrolü
            var currentUser = App.CurrentUser;
            if (currentUser != null && currentUser.IsAdmin)
            {
                AdminTab.IsVisible = true; // Admin ise göster
            }
            else
            {
                AdminTab.IsVisible = false; // Değilse gizle
            }
        }
    }
}