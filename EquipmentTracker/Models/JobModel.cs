// Dosya: Models/JobModel.cs
using EquipmentTracker.Models.Enums;
using System.Collections.ObjectModel; // List yerine
namespace EquipmentTracker.Models
{
    public class JobModel
    {
        public int Id { get; set; } // Veritabanı Anahtarı (Primary Key)
        public string JobNumber { get; set; }
        public string JobOwner { get; set; }
        public string JobName { get; set; }
        public DateTime Date { get; set; }

        // --- YENİ EKLENEN ALANLAR ---

        /// <summary>
        /// İşi oluşturan kişinin adı
        /// </summary>
        public string CreatorName { get; set; }

        /// <summary>
        /// İşi oluşturanın rolü/yetkisi
        /// </summary>
        public string CreatorRole { get; set; }

        /// <summary>
        /// İş için girilen genel açıklama
        /// </summary>
        public string JobDescription { get; set; }

        /// <summary>
        /// Bu işin ana onay durumu
        /// </summary>
        public ApprovalStatus MainApproval { get; set; }

        // --- MEVCUT KOLEKSİYON ---
        public ObservableCollection<Equipment> Equipments { get; set; } = new();

        // Constructor'ı varsayılan durumu ayarlayacak şekilde güncelleyelim
        public JobModel()
        {
            MainApproval = ApprovalStatus.Pending; // Yeni işler beklemede başlar
        }
    }
}