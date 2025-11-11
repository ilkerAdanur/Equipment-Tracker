// Dosya: Models/JobModel.cs
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

        // Bu, listeye yeni bir ekipman eklediğimizde UI'ın anında güncellenmesini sağlar.
        public ObservableCollection<Equipment> Equipments { get; set; } = new();
    }
}