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
        Task<EquipmentPart> AddNewPartAsync(Equipment parentEquipment, EquipmentPart newPart);
        Task<(string nextEquipId, string nextEquipCode)> GetNextEquipmentIdsAsync(JobModel parentJob);
        Task<(string nextPartId, string nextPartCode)> GetNextPartIdsAsync(Equipment parentEquipment);
    }
}
