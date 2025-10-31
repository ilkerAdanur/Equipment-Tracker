using System.Collections.Generic;

namespace EquipmentTracker.Models
{
    public class JobModel
    {
        public int Id { get; set; } // Veritabanı için (örn: 1, 2, 3)
        public string JobNumber { get; set; } // Sizin istediğiniz numara (örn: 001)
        public string JobOwner { get; set; } // Örn: STH ÇEVRE
        public string JobName { get; set; } // Örn: AŞKALE ÇİMENTO...
        public DateTime Date { get; set; }

        // Hiyerarşi: Bir İş, birçok Ekipman'dan oluşur.
        public List<Equipment> Equipments { get; set; } = new List<Equipment>();
    }
}