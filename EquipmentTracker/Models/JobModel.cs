using CommunityToolkit.Mvvm.ComponentModel; // EKLENDİ
using EquipmentTracker.Models.Enums;
using System.Collections.ObjectModel;

namespace EquipmentTracker.Models
{
    // ObservableObject eklendi ve partial yapıldı
    public partial class JobModel : ObservableObject
    {
        public int Id { get; set; }
        public string JobNumber { get; set; }
        public string JobName { get; set; }
        public string JobOwner { get; set; }
        public DateTime Date { get; set; }
        public string JobDescription { get; set; }
        public ApprovalStatus MainApproval { get; set; }

        // YENİ: ObservableProperty olarak tanımlandı.
        // Bu sayede değer değiştiği an ekrandaki buton rengi değişecek.
        [ObservableProperty]
        private bool _isCancelled;

        public ObservableCollection<Equipment> Equipments { get; set; } = new();

        public JobModel()
        {
            MainApproval = ApprovalStatus.Pending;
            IsCancelled = false;
        }
    }
}