namespace EquipmentTracker.Models
{
    public class Equipment
    {
        public string EquipmentId { get; set; } // Örn: 001
        public string EquipmentCode { get; set; } // Örn: 001.001
        public string Name { get; set; } // Örn: KARIŞTIRICI TANKI

        // Hiyerarşi: Bir Ekipman, birçok Parça'dan oluşur.
        public List<EquipmentPart> Parts { get; set; } = new List<EquipmentPart>();
    }
}