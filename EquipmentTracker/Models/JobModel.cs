using CommunityToolkit.Mvvm.ComponentModel;
using EquipmentTracker.Models.Enums;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentTracker.Models
{
    // Partial olması şarttır (Toolkit kod üretebilsin diye)
    public partial class JobModel : ObservableObject
    {
        public int Id { get; set; }

        [ObservableProperty]
        private string _jobNumber;

        [ObservableProperty]
        private string _jobName;

        [ObservableProperty]
        private string _jobOwner;

        [ObservableProperty]
        private DateTime _date;

        [ObservableProperty]
        private string _jobDescription;

        // Onay durumu için veritabanı sütun adını koruyoruz
        [Column("ApprovalStatus")]
        public ApprovalStatus MainApproval { get; set; } = ApprovalStatus.Pending; // Varsayılan değer

        [ObservableProperty]
        private bool _isCancelled;

        // İlişkili Tablolar
        public ObservableCollection<Equipment> Equipments { get; set; } = new();

        public JobModel()
        {
            // Varsayılan değerler
            Date = DateTime.Now;
            IsCancelled = false;
            MainApproval = ApprovalStatus.Pending;
        }
    }
}