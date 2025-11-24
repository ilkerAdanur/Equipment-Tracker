using CommunityToolkit.Mvvm.ComponentModel; // EKLENDİ
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace EquipmentTracker.Models
{
    // ObservableObject eklendi ve partial yapıldı
    public partial class Equipment : ObservableObject
    {
        public int Id { get; set; }
        public string? EquipmentId { get; set; }
        public string? EquipmentCode { get; set; }
        public string? Name { get; set; }

        // YENİ: İptal Durumu (Observable)
        [ObservableProperty]
        private bool _isCancelled;

        public ObservableCollection<EquipmentPart> Parts { get; set; } = new();
        public ObservableCollection<EquipmentAttachment> Attachments { get; set; } = new();

        public int JobId { get; set; }
        [JsonIgnore]
        public JobModel? Job { get; set; }
    }
}