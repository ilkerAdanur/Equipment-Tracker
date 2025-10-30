using Foundation;
using WorkflowManagement.Views;

namespace WorkflowManagement
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => Views.MainPage.CreateMauiApp();
    }
}
