using EquipmentTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EquipmentTracker.Services.EquipmentService
{
    public interface IEquipmentService
    {
        Task<Equipment> AddEquipmentAsync(JobModel parentJob, Equipment newEquipment);
        Task<(string nextEquipId, string nextEquipCode)> GetNextEquipmentIdsAsync(JobModel parentJob);
        Task DeleteEquipmentAsync(int equipmentId);
        Task ToggleEquipmentStatusAsync(int equipmentId, bool isCancelled);
        Task UpdateEquipmentNameAsync(Equipment equipment, string newName);
    }
}
