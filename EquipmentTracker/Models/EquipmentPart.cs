// Dosya: Models/EquipmentPart.cs
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace EquipmentTracker.Models
{
    public partial class EquipmentPart : ObservableObject
    {
        public int Id { get; set; }
        public string? PartId { get; set; }
        public string? PartCode { get; set; }
        public string? Name { get; set; }

        // Veritabanı İlişkisi (Foreign Key)
        public int EquipmentId { get; set; }
        [JsonIgnore]
        public Equipment? Equipment { get; set; }

        [ObservableProperty]
        private bool _isCancelled;

        // YENİ EKLENEN ÖZELLİK:
        /// <summary>
        /// Bu parçaya bağlı dosyaların (teknik resim, vb.) listesi.
        /// </summary>
        public ObservableCollection<EquipmentPartAttachment> Attachments { get; set; } = new();
    }
}