using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentTracker.Models
{
    public partial class EquipmentAttachment : ObservableObject
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }

        [ObservableProperty] 
        private string? _thumbnailPath;

        public int EquipmentId { get; set; }
        public Equipment Equipment { get; set; }

        // UI Durumları
        [NotMapped]
        [ObservableProperty]
        private bool _isProcessing;

        [NotMapped]
        [ObservableProperty]
        private double _processingProgress;
    }
}