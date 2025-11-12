using EquipmentTracker.Data;
using EquipmentTracker.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EquipmentTracker.Services.EquipmentPartService
{
    class EquipmentPartService : IEquipmentPartService
    {
        private readonly DataContext _context;

        public EquipmentPartService(DataContext context)
        {
            _context = context;
        }
        public async Task<EquipmentPart> AddNewPartAsync(Equipment parentEquipment, EquipmentPart newPart)
        {
            newPart.EquipmentId = parentEquipment.Id;
            _context.EquipmentParts.Add(newPart);
            await _context.SaveChangesAsync();

            _context.Entry(newPart).State = EntityState.Detached;

            return newPart;
        }
        public async Task<(string nextPartId, string nextPartCode)> GetNextPartIdsAsync(Equipment parentEquipment)
        {
            string equipCode = parentEquipment.EquipmentCode;

            var allPartsForEquipment = await _context.EquipmentParts
                .Where(p => p.EquipmentId == parentEquipment.Id)
                .ToListAsync();

            // Sıralamayı C# içinde yap (string "10"un "2"den büyük olduğunu anlaması için)
            var lastPart = allPartsForEquipment
                .Where(p => int.TryParse(p.PartId, out _)) // Sadece sayısal PartId'leri al
                .OrderByDescending(p => int.Parse(p.PartId)) // Bunları sayısal olarak sırala
                .FirstOrDefault(); // En yükseğini al

            int nextId = 1;
            if (lastPart != null && int.TryParse(lastPart.PartId, out int lastId))
            {
                nextId = lastId + 1;
            }

            // GÜNCELLENDİ: PartId'yi "D3" (001, 002, ...) olarak formatla
            string nextPartId = nextId.ToString("D3");
            string nextPartCode = $"{equipCode}.{nextPartId}"; // "001.001.001"

            return (nextPartId, nextPartCode);
        }
        public async Task DeleteEquipmentPart(int partId)
        {
            var partToDelete = await _context.EquipmentParts.FindAsync(partId);

            if (partToDelete != null)
            {
                _context.EquipmentParts.Remove(partToDelete);
                await _context.SaveChangesAsync();
            }
        }
    }
}