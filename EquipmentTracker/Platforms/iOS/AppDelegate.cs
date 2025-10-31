using Foundation;
using EquipmentTracker.Views;

namespace EquipmentTracker
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override Microsoft.Maui.Hosting.MauiApp CreateMauiApp() => Views.MainPage.CreateMauiApp();
    }
}
