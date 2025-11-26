using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EquipmentTracker.Models
{
    public partial class EquipmentPartAttachment : ObservableObject
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }

        [ObservableProperty]
        private string? _thumbnailPath;

        public int EquipmentPartId { get; set; }

        [JsonIgnore]
        public EquipmentPart EquipmentPart { get; set; }


        [NotMapped]
        [ObservableProperty]
        private bool _isProcessing;

        [NotMapped]
        [ObservableProperty]
        private double _processingProgress; 
    }
}