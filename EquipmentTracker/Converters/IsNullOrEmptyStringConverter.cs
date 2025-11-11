// Dosya: Converters/IsNullOrEmptyStringConverter.cs
using System.Globalization;

namespace EquipmentTracker.Converters
{
    public class IsNullOrEmptyStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Gelen değerin (ThumbnailPath) null veya boş olup olmadığını kontrol et
            return string.IsNullOrEmpty(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Bu projede 'ConvertBack' (geri dönüştürme) gerekmiyor
            throw new NotImplementedException();
        }
    }
}