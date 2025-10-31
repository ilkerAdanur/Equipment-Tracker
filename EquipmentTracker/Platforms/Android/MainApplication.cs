using Android.App;
using Android.Runtime;
using EquipmentTracker.Views;

namespace EquipmentTracker
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        protected override Microsoft.Maui.Hosting.MauiApp CreateMauiApp() => Views.MainPage.CreateMauiApp();
    }
}
