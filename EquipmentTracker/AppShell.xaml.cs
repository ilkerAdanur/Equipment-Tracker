// Dosya: AppShell.xaml.cs
using EquipmentTracker.Views;

namespace EquipmentTracker
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Yeni rotaları (sayfaları) sisteme tanıt
            Routing.RegisterRoute(nameof(JobDetailsPage), typeof(JobDetailsPage));
            Routing.RegisterRoute(nameof(AddNewJobPage), typeof(AddNewJobPage));
        }
    }
}