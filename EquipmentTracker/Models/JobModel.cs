using CommunityToolkit.Mvvm.ComponentModel;
using EquipmentTracker.Models.Enums;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema; // <-- BU KÜTÜPHANEYİ EKLEMELİSİN

namespace EquipmentTracker.Models
{
    public partial class JobModel : ObservableObject
    {
        public int Id { get; set; }
        public string JobNumber { get; set; }
        public string JobName { get; set; }
        public string JobOwner { get; set; }
        public DateTime Date { get; set; }
        public string JobDescription { get; set; }

        // --- DÜZELTME BURADA ---
        // Veritabanındaki sütun adı 'ApprovalStatus' olduğu için bunu belirtiyoruz.
        [Column("ApprovalStatus")]
        public ApprovalStatus MainApproval { get; set; }

        // Veritabanında 'IsCancelled' sütunu 0/1 (tinyint) olarak tutulur, MySQL bunu bool'a çevirir.
        [ObservableProperty]
        private bool _isCancelled;

        // Not: Veritabanı resminde 'RejectionReason' sütunu da var ama modelinde yok.
        // İstersen onu da ekleyebilirsin:
        // public string? RejectionReason { get; set; }

        public ObservableCollection<Equipment> Equipments { get; set; } = new();

        public JobModel()
        {
            MainApproval = ApprovalStatus.Pending;
            IsCancelled = false;
        }
    }
}