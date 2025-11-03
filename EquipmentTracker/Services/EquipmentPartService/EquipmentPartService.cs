using EquipmentTracker.Data;
using EquipmentTracker.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            newPart.EquipmentId = parentEquipment.Id; // Foreign key'i (ilişkiyi) ayarla
            _context.EquipmentParts.Add(newPart);
            await _context.SaveChangesAsync();

            _context.Entry(newPart).State = EntityState.Detached;

            return newPart;
        }
        public async Task<(string nextPartId, string nextPartCode)> GetNextPartIdsAsync(Equipment parentEquipment)
        {
            // Bu parçanın bağlı olduğu EKİPMANIN koduna ("001.001") ihtiyacımız var
            string equipCode = parentEquipment.EquipmentCode;

            // O ekipmana bağlı en son parçayı bul
            var lastPart = await _context.EquipmentParts
                .Where(p => p.EquipmentId == parentEquipment.Id)
                .OrderByDescending(p => p.PartId)
                .FirstOrDefaultAsync();

            int nextId = 1;
            if (lastPart != null && int.TryParse(lastPart.PartId, out int lastId))
            {
                nextId = lastId + 1;
            }

            string nextPartId = nextId.ToString(); // "1", "2", "3"...
            string nextPartCode = $"{equipCode}.{nextId}"; // "001.001.1", "001.001.2"...

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
