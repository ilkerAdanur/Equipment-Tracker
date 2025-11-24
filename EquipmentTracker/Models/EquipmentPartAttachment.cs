using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EquipmentTracker.Models
{
    // ObservableObject eklendi ve partial yapıldı
    public partial class EquipmentPartAttachment : ObservableObject
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }

        // Resim yolu değiştiğinde arayüzü uyarmak için ObservableProperty
        [ObservableProperty]
        private string? _thumbnailPath;

        public int EquipmentPartId { get; set; }

        [JsonIgnore]
        public EquipmentPart EquipmentPart { get; set; }

        // --- YENİ EKLENEN UI ÖZELLİKLERİ (Veritabanına Kaydedilmez) ---

        [NotMapped]
        [ObservableProperty]
        private bool _isProcessing; // İşlem devam ediyor mu?

        [NotMapped]
        [ObservableProperty]
        private double _processingProgress; // 0.0 ile 1.0 arası
    }
}