using Android.App;
using Android.Runtime;
using WorkflowManagement.Views;

namespace WorkflowManagement
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        protected override MauiApp CreateMauiApp() => Views.MainPage.CreateMauiApp();
    }
}
