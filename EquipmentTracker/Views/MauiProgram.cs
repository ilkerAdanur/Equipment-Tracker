using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;
using EquipmentTracker;
using EquipmentTracker.Data;
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
using MySqlConnector;

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

        // --- VERİTABANI BAĞLANTI AYARI (MySQL - Hostinger) ---
        builder.Services.AddDbContext<DataContext>(options =>
        {
            string serverIp = Preferences.Get("ServerIP", "equipmenttracker.ilkeradanur.com");
            string dbUser = Preferences.Get("DbUser", "u993098094_TrackerUser");
            string dbPass = Preferences.Get("DbPassword", "");

            // Eğer IP boşsa bağlanma
            if (string.IsNullOrWhiteSpace(serverIp)) return;

            // MySQL Bağlantı Cümlesi
            var connectionBuilder = new MySqlConnectionStringBuilder
            {
                Server = serverIp,
                Database = "u993098094_TrackerDB",
                UserID = dbUser,
                Password = dbPass,
                Port = 3306,
                SslMode = MySqlSslMode.None,
                ConnectionTimeout = 10
            };

            // DÜZELTME BURADA: UseSqlServer YERİNE UseMySql
            options.UseMySql(
                connectionBuilder.ConnectionString,
                ServerVersion.AutoDetect(connectionBuilder.ConnectionString)
            );
        });

        // --- DİĞER SERVİSLER (Aynen kalıyor) ---
        builder.Services.AddTransient<IJobService, JobService>();
        builder.Services.AddTransient<IEquipmentService, EquipmentService>();
        builder.Services.AddTransient<IEquipmentPartService, EquipmentPartService>();
        builder.Services.AddTransient<IAttachmentService, AttachmentService>();
        builder.Services.AddTransient<IEquipmentPartAttachmentService, EquipmentPartAttachmentService>();
        builder.Services.AddTransient<IStatisticsService, StatisticsService>();
        builder.Services.AddTransient<IAuthService, AuthService>();


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