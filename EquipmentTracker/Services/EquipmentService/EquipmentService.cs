// Dosya: Services/Equipment/EquipmentService.cs
using EquipmentTracker.Data;
using EquipmentTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace EquipmentTracker.Services.EquipmentService
{
    public class EquipmentService : IEquipmentService
    {
        private readonly DataContext _context;

        public EquipmentService(DataContext context)
        {
            _context = context;
        }


        public async Task<Equipment> AddEquipmentAsync(JobModel parentJob, Equipment newEquipment)
        {
            newEquipment.JobId = parentJob.Id; // Foreign key'i (ilişkiyi) ayarla
            _context.Equipments.Add(newEquipment);
            await _context.SaveChangesAsync();

            _context.Entry(newEquipment).State = EntityState.Detached;

            return newEquipment;
        }
        public async Task<(string nextEquipId, string nextEquipCode)> GetNextEquipmentIdsAsync(JobModel parentJob)
        {
            string jobNum = parentJob.JobNumber;

            var lastEquipment = await _context.Equipments
                .Where(e => e.JobId == parentJob.Id)
                .OrderByDescending(e => e.EquipmentId) 
                .FirstOrDefaultAsync();

            int nextId = 1;
            if (lastEquipment != null && int.TryParse(lastEquipment.EquipmentId, out int lastId))
            {
                nextId = lastId + 1;
            }

            string nextEquipId = nextId.ToString("D3"); 
            string nextEquipCode = $"{jobNum}.{nextEquipId}"; 

            return (nextEquipId, nextEquipCode);
        }

        public async Task DeleteEquipmentAsync(int equipmentId)
        {
            var equipmentToDelete = await _context.Equipments.FindAsync(equipmentId);

            if (equipmentToDelete != null)
            {
                _context.Equipments.Remove(equipmentToDelete);
                await _context.SaveChangesAsync();
            }
        }

    }
}