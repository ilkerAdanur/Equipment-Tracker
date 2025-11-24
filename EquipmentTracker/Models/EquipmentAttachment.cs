// Dosya: Models/EquipmentAttachment.cs
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EquipmentTracker.Models
{

    public partial class EquipmentAttachment : ObservableObject
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }

        // DEĞİŞİKLİK BURADA: 'string' yanına '?' ekleyin
        public string? ThumbnailPath { get; set; }

        public int EquipmentId { get; set; }
        [JsonIgnore]
        public Equipment Equipment { get; set; }
        [NotMapped]
        [ObservableProperty]
        private bool _isProcessing; // İşlem devam ediyor mu?

        [NotMapped]
        [ObservableProperty]
        private double _processingProgress;
    }
}