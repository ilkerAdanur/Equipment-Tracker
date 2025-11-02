// Dosya: Models/Equipment.cs
using System.Collections.ObjectModel;
using System.Text.Json.Serialization; 

namespace EquipmentTracker.Models
{
    public class Equipment
    {
        public int Id { get; set; } 
        public string? EquipmentId { get; set; }
        public string? EquipmentCode { get; set; }
        public string? Name { get; set; }

        // Hiyerarşi (ObservableCollection olarak güncellendi)
        public ObservableCollection<EquipmentPart> Parts { get; set; } = new();

        // Veritabanı İlişkisi (Foreign Key)
        public int JobId { get; set; }
        [JsonIgnore] // JSON serileştirmede döngüleri engeller
        public JobModel? Job { get; set; }
    }
}