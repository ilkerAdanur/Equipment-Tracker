using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;
using EquipmentTracker;
using EquipmentTracker.Data;
using EquipmentTracker.Services;
using EquipmentTracker.Services.AttachmentServices;
using EquipmentTracker.Services.Auth;
using EquipmentTracker.Services.EquipmentPartAttachmentServices;
using EquipmentTracker.Services.EquipmentPartService;
using EquipmentTracker.Services.EquipmentService;
using EquipmentTracker.Services.Job;
using EquipmentTracker.Services.StatisticsService;
using EquipmentTracker.Services.StatisticsServices;
using EquipmentTracker.ViewModels;
using EquipmentTracker.Views;
using Microsoft.EntityFrameworkCore;

public static class MauiProgram
{
    public static bool IsDatabaseInitialized { get; set; } = false;

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // --- SQL SERVER BAĞLANTI AYARI ---

        // ÖNEMLİ: Aşağıdaki IP Adresi (192.168.1.XXX), SQL Server'ın kurulu olduğu bilgisayarın IP'si olmalı.
        // Eğer uygulamayı SQL Server'ın olduğu bilgisayarda çalıştırıyorsanız "Server=localhost" yeterli.
        // Uzaktan erişim için: "Server=192.168.1.20,1433;Database=TrackerDB;User Id=sa;Password=Sifreniz123;TrustServerCertificate=True;"

        // Şimdilik local geliştirme için (SSMS ile bağlanacağınız):
        // Trusted_Connection=True; -> Windows Authentication kullanır (Kullanıcı adı şifre sormaz)
        // TrustServerCertificate=True; -> SSL sertifikası hatasını engeller.

        // SQL Server Kullanımı:
        builder.Services.AddDbContext<DataContext>(options =>
        {
            string serverIp = Preferences.Get("ServerIP", "");
            string dbName = Preferences.Get("DbName", "");
            string dbUser = Preferences.Get("DbUser", "");
            string dbPass = Preferences.Get("DbPassword", "");

            if (string.IsNullOrWhiteSpace(serverIp) || string.IsNullOrWhiteSpace(dbName))
            {
                return;
            }

            string connectionString = $"Server={serverIp};Database={dbName};User={dbUser};Password={dbPass};Port=3306;ConnectionTimeout=30;";

            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        });

        // --- DİĞER SERVİSLER (Aynen kalıyor) ---
        builder.Services.AddTransient<IJobService, JobService>();
        builder.Services.AddTransient<IEquipmentService, EquipmentService>();
        builder.Services.AddTransient<IEquipmentPartService, EquipmentPartService>();
        builder.Services.AddTransient<IAttachmentService, AttachmentService>();
        builder.Services.AddTransient<IEquipmentPartAttachmentService, EquipmentPartAttachmentService>();
        builder.Services.AddTransient<IStatisticsService, StatisticsService>();
        builder.Services.AddTransient<IAuthService, AuthService>();

        builder.Services.AddSingleton<FtpHelper>();
        builder.Services.AddSingleton<IFolderPicker>(FolderPicker.Default);

        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<JobDetailsViewModel>();
        builder.Services.AddTransient<AddNewJobViewModel>();
        builder.Services.AddTransient<JobListViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        builder.Services.AddTransient<JobDetailsPage>();
        builder.Services.AddTransient<AddNewJobPage>();
        builder.Services.AddTransient<JobListPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<SettingsPage>();

        return builder.Build();
    }
}