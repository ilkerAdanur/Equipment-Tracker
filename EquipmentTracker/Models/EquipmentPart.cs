// Dosya: Models/EquipmentPart.cs
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace EquipmentTracker.Models
{
    public class EquipmentPart
    {
        public int Id { get; set; }
        public string PartId { get; set; }
        public string PartCode { get; set; }
        public string Name { get; set; }

        // Veritabanı İlişkisi (Foreign Key)
        public int EquipmentId { get; set; }
        [JsonIgnore]
        public Equipment Equipment { get; set; }

        // YENİ EKLENEN ÖZELLİK:
        /// <summary>
        /// Bu parçaya bağlı dosyaların (teknik resim, vb.) listesi.
        /// </summary>
        public ObservableCollection<EquipmentPartAttachment> Attachments { get; set; } = new();
    }
}