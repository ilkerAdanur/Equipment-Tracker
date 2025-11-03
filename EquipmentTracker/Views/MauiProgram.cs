// Dosya: MauiProgram.cs
using CommunityToolkit.Maui;
using EquipmentTracker;
using EquipmentTracker.Data; // DataContext için bunu ekleyin
using EquipmentTracker.Services.AttachmentServices;
using EquipmentTracker.Services.EquipmentPartAttachmentServices;
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
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        // 2. Onun içine 'TrackerDatabase' adında bir klasör yolu oluştur
        string appDataDirectory = Path.Combine(documentsPath, "TrackerDatabase");

        // 3. Bu klasörün var olduğundan emin ol (yoksa oluştur)
        if (!Directory.Exists(appDataDirectory))
        {
            Directory.CreateDirectory(appDataDirectory);
        }

        // 4. Veritabanı dosyasının tam yolunu oluştur
        string dbPath = Path.Combine(appDataDirectory, "tracker.db");


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
        builder.Services.AddSingleton<IAttachmentService, AttachmentService>();
        builder.Services.AddSingleton<IEquipmentPartAttachmentService, EquipmentPartAttachmentService>();

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