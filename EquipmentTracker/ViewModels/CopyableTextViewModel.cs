using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace EquipmentTracker.ViewModels;

public partial class CopyableTextViewModel : ObservableObject
{
    private string _originalText;
    private string _currentDisplayText;
    private bool _isCopying;
    [ObservableProperty]
    string _text;

    public string CurrentDisplayText
    {
        get => _currentDisplayText;
        set => SetProperty(ref _currentDisplayText, value);
    }

    // YENİ ÖZELLİK: Kopyalama durumu
    public bool IsCopying
    {
        get => _isCopying;
        set => SetProperty(ref _isCopying, value);
    }

    public CopyableTextViewModel(string text)
    {
        text = text ?? string.Empty;
        _originalText = text;
        _currentDisplayText = text;
    }

    // Bu, komutun çağıracağı metot olacak.
    [RelayCommand]
    public async Task CopyAndNotifyAsync(object parameter)
    {
        if (IsCopying) return;
        IsCopying = true;

        try
        {
            // 1. Kopyalanacak metni belirle ve panoya kopyala
            string textToCopy = _originalText;

            // DateTime tipiyse özel formatlama yap (XAML'deki FormatString'i burada uygulamalıyız)
            if (parameter is DateTime dateTime)
            {
                textToCopy = dateTime.ToString("dd.MM.yyyy");
            }

            await Clipboard.SetTextAsync(textToCopy);

            // 2. Geri bildirim: Metni değiştir
            CurrentDisplayText = "Kopyalandı! ✔️";

            // 3. Bekle
            await Task.Delay(1200);

            // 4. Metni geri yükle
            CurrentDisplayText = _originalText;
        }
        finally
        {
            IsCopying = false;
        }
    }
}