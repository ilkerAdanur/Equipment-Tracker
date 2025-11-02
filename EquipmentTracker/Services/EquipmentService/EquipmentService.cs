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
            return newEquipment;
        }


        public async Task<EquipmentPart> AddNewPartAsync(Equipment parentEquipment, EquipmentPart newPart)
        {
            newPart.EquipmentId = parentEquipment.Id; 
            _context.EquipmentParts.Add(newPart);
            await _context.SaveChangesAsync();
            return newPart;
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

        public async Task<(string nextPartId, string nextPartCode)> GetNextPartIdsAsync(Equipment parentEquipment)
        {
            string equipCode = parentEquipment.EquipmentCode;

            var lastPart = await _context.EquipmentParts
                .Where(p => p.EquipmentId == parentEquipment.Id)
                .OrderByDescending(p => p.PartId) 
                .FirstOrDefaultAsync();

            int nextId = 1;
            if (lastPart != null && int.TryParse(lastPart.PartId, out int lastId))
            {
                nextId = lastId + 1;
            }

            string nextPartId = nextId.ToString(); 
            string nextPartCode = $"{equipCode}.{nextPartId}"; 

            return (nextPartId, nextPartCode);
        }
    }
}