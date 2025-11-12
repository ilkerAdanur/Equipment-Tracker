// Dosya: Views/MauiProgram.cs
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;
using EquipmentTracker;
using EquipmentTracker.Data;
using EquipmentTracker.Services.AttachmentServices;
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
    // YENİ EKLENDİ: Veritabanının hazır olup olmadığını takip etmek için
    public static bool IsDatabaseInitialized { get; set; } = false;

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                // ... fontlar ...
            });

        // ... (VERİTABANI YOLU AYARLAMA bölümü aynı kalıyor) ...
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string appDataDirectory = Path.Combine(documentsPath, "TrackerDatabase");
        if (!Directory.Exists(appDataDirectory))
        {
            Directory.CreateDirectory(appDataDirectory);
        }
        string dbPath = Path.Combine(appDataDirectory, "tracker.db");

        // --- BAĞIMLILIK KAYITLARI ---

        // 1. Veritabanı (DbContext) Kaydı:
        builder.Services.AddDbContext<DataContext>(options =>
            options.UseSqlite($"Data Source={dbPath}")
        );

        // 2. Servisler
        builder.Services.AddSingleton<IJobService, JobService>();
        builder.Services.AddSingleton<IEquipmentService, EquipmentService>();
        builder.Services.AddSingleton<IEquipmentPartService, EquipmentPartService>();
        builder.Services.AddSingleton<IAttachmentService, AttachmentService>();
        builder.Services.AddSingleton<IEquipmentPartAttachmentService, EquipmentPartAttachmentService>();
        builder.Services.AddSingleton<IStatisticsService, StatisticsService>();

        builder.Services.AddSingleton<IFolderPicker>(FolderPicker.Default);

        // 3. ViewModellar
        builder.Services.AddTransient<JobDetailsViewModel>();
        builder.Services.AddTransient<AddNewJobViewModel>();
        builder.Services.AddTransient<JobListViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();


        // 4. View'ler
        builder.Services.AddTransient<JobDetailsPage>();
        builder.Services.AddTransient<AddNewJobPage>();
        builder.Services.AddTransient<JobListPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<SettingsPage>();

        // --- SİLİNEN BÖLÜM ---
        // 'var app = builder.Build();' ile başlayan
        // ve 'jobService.InitializeDatabaseAsync().Wait();' içeren
        // tüm bloğu buradan SİLİN.

        // Sadece bu satır kalsın:
        return builder.Build();
    }
}