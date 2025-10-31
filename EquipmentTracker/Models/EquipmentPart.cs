// Dosya: Models/EquipmentPart.cs
using System.Text.Json.Serialization;

namespace EquipmentTracker.Models
{
    public class EquipmentPart
    {
        public int Id { get; set; } // Veritabanı Anahtarı (Primary Key)
        public string PartId { get; set; }
        public string PartCode { get; set; }
        public string Name { get; set; }

        // Veritabanı İlişkisi (Foreign Key)
        public int EquipmentId { get; set; }
        [JsonIgnore]
        public Equipment Equipment { get; set; }
    }
}