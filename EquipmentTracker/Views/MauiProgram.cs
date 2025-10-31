// Dosya: MauiProgram.cs
using CommunityToolkit.Maui;
using EquipmentTracker.Services.Job;
using EquipmentTracker;
using EquipmentTracker.ViewModels;      // ViewModellarımız için
using EquipmentTracker.Views;         // Sayfalarımız için
using CommunityToolkit.Mvvm.ComponentModel;

namespace EquipmentTracker.Views;
public static class MauiProgram
{
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

        // --- BAĞIMLILIK KAYITLARI ---

        // 1. Servisler: Uygulama boyunca tek bir JobService nesnesi yaşasın.
        builder.Services.AddSingleton<IJobService, JobService>();

        // 2. ViewModellar: Her sayfa için yeni bir ViewModel oluşturulsun.
        builder.Services.AddTransient<JobDetailsViewModel>();

        // 3. View'ler (Sayfalar): Her istendiğinde yeni bir Sayfa oluşturulsun.
        builder.Services.AddTransient<JobDetailsPage>();

        // Not: Ekran görüntünüzdeki eski 'MainPage' artık kullanılmayacak.
        // Onu silebilirsiniz veya bırakabilirsiniz, zararı yok.

        return builder.Build();
    }
}