using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EquipmentTracker.Models
{
    /// <summary>
 /// Dashboard'da gösterilecek hesaplanmış istatistikleri tutar.
 /// </summary>
    public class Statistics
    {
        public int TotalJobs { get; set; }
        public int TotalEquipments { get; set; }
        public int TotalParts { get; set; }
        public int TotalAttachments { get; set; } // Ekipman + Parça Dosyaları

        // Onay durumlarına göre iş sayıları
        public int PendingJobs { get; set; }
        public int ApprovedJobs { get; set; }
        public int RejectedJobs { get; set; }

        // En'ler
        public string JobWithMostEquipment { get; set; }
        public string JobWithMostParts { get; set; }
    }
}
