using EquipmentTracker.Models;

namespace EquipmentTracker.Services.EquipmentPartService
{
    public interface IEquipmentPartService
    {
        Task<EquipmentPart> AddNewPartAsync(Equipment parentEquipment, EquipmentPart newPart);
        Task<(string nextPartId, string nextPartCode)> GetNextPartIdsAsync(Equipment parentEquipment);
        Task DeleteEquipmentPart(int partId);
        Task TogglePartStatusAsync(int partId, bool isCancelled);
        Task UpdateEquipmentPartNameAsync(EquipmentPart part, string newName);
    }
}