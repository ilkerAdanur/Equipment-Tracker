using System.ComponentModel.DataAnnotations;

namespace EquipmentTracker.Models
{
    public class AppSetting
    {
        [Key]
        public int Id { get; set; }
        public string Key { get; set; }   // Ayar Adı (Örn: "AttachmentPath")
        public string Value { get; set; } // Değeri (Örn: "\\192.168.1.20\TrackerData")
    }
}