// Dosya: MauiProgram.cs
using CommunityToolkit.Maui;
using EquipmentTracker;
using EquipmentTracker.Data; // DataContext için bunu ekleyin
using EquipmentTracker.Services.EquipmentPartService;
using EquipmentTracker.Services.EquipmentService;
using EquipmentTracker.Services.Job;
using EquipmentTracker.ViewModels;
using EquipmentTracker.Views;
using Microsoft.EntityFrameworkCore; // AddDbContext ve UseSqlite için bunu ekleyin
// ... (diğer using'ler)

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit() // Bu zaten olmalı
            .ConfigureFonts(fonts =>
            {
                // ... fontlar ...
            });

        // --- VERİTABANI YOLUNU AYARLAMA ---
        // Veritabanı dosyasını cihazın kendi yerel depolama alanına koymak en güvenli yoldur.
        // Örn: C:\Users\[KullaniciAdi]\AppData\Local\Packages\[...]\LocalState\tracker.db
        string dbDirectory = "C://TrackerDatabase"; // 1. Klasör yolunu ayır
        string dbPath = Path.Combine(dbDirectory, "tracker.db");

        if (!Directory.Exists(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }


        // --- BAĞIMLILIK KAYITLARI ---

        // 1. Veritabanı (DbContext) Kaydı:
        // Uygulamaya DataContext'i ve SQLite kullanacağını, yolunun da bu olduğunu söylüyoruz.
        builder.Services.AddDbContext<DataContext>(options =>
            options.UseSqlite($"Data Source={dbPath}")
        );

        // 2. Servisler (Mevcut kodunuzdaki gibi)
        builder.Services.AddSingleton<IJobService, JobService>();
        builder.Services.AddSingleton<IEquipmentService, EquipmentService>();
        builder.Services.AddSingleton<IEquipmentPartService, EquipmentPartService>();

        // 3. ViewModellar (Mevcut kodunuzdaki gibi)
        builder.Services.AddTransient<JobDetailsViewModel>();
        builder.Services.AddTransient<AddNewJobViewModel>();
        builder.Services.AddTransient<JobListViewModel>();


        // 4. View'ler (Mevcut kodunuzdaki gibi)
        builder.Services.AddTransient<JobDetailsPage>();
        builder.Services.AddTransient<AddNewJobPage>();
        builder.Services.AddTransient<JobListPage>();

        return builder.Build();
    }
}