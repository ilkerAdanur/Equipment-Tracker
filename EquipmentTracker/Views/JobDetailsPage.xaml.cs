using EquipmentTracker.ViewModels;
using System.Diagnostics;

namespace EquipmentTracker.Views;

public partial class JobDetailsPage : ContentPage
{
    // Scroll pozisyonunu tutacak değişken
    private double _lastScrollY = 0;

    public JobDetailsPage(JobDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    // Sayfadan başka uygulamaya geçince veya geri çıkınca çalışır
    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Mevcut kaydırma pozisyonunu kaydet
        _lastScrollY = MainScroll.ScrollY;
    }

    // Sayfaya geri dönünce çalışır
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // ViewModel veriyi yüklüyor olabilir, biraz bekleyelim
        // Eğer ViewModel'inizde OnAppearing load işlemi varsa, o listenin oluşması zaman alır.
        if (BindingContext is JobDetailsViewModel vm)
        {
            // Eğer veri yoksa yükle, varsa elleme (Burası opsiyonel iyileştirme)
            // await vm.LoadDataCommand.ExecuteAsync(null); 
        }

        // UI'ın kendine gelmesi için çok kısa bekle
        await Task.Delay(100);

        // Kaydedilen pozisyona geri git (Animasyonsuz: false)
        if (_lastScrollY > 0)
        {
            await MainScroll.ScrollToAsync(0, _lastScrollY, false);
        }
    }
}