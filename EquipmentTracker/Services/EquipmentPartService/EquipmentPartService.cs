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
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                // 1. Sıradaki Parça Numarasını Hesapla
                var allParts = await _context.EquipmentParts
                    .Where(p => p.EquipmentId == parentEquipment.Id)
                    .Select(p => p.PartId)
                    .ToListAsync();

                int maxId = 0;
                foreach (var idStr in allParts)
                {
                    if (int.TryParse(idStr, out int id))
                    {
                        if (id > maxId) maxId = id;
                    }
                }
                int nextId = maxId + 1;

                // 2. Numaraları Ata
                newPart.PartId = nextId.ToString();
                newPart.PartCode = $"{parentEquipment.EquipmentCode}.{nextId}";
                newPart.EquipmentId = parentEquipment.Id;

                // 3. Veritabanına Kaydet
                _context.EquipmentParts.Add(newPart);
                await _context.SaveChangesAsync();

                _context.Entry(newPart).State = EntityState.Detached;

                // --- YENİ EKLENEN KISIM: KLASÖR OLUŞTURMA ---
                try
                {
                    // A. Ortak Yolu (Veritabanından) Belirle
                    string dbPath;
                    var setting = await _context.AppSettings
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Key == "AttachmentPath");

                    if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
                        dbPath = setting.Value;
                    else
                        dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase");

                    // B. Ana Klasör Yolu
                    string baseAttachmentPath = Path.Combine(dbPath, "Attachments");

                    // C. Üst İş (Job) Bilgisini Çek (Klasör isimleri için gerekli)
                    var parentJob = await _context.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == parentEquipment.JobId);

                    if (parentJob != null)
                    {
                        // D. İsimleri Temizle
                        string safeJobName = SanitizeFolderName(parentJob.JobName);
                        string safeEquipName = SanitizeFolderName(parentEquipment.Name);
                        string safePartName = SanitizeFolderName(newPart.Name);

                        // E. Klasör Hiyerarşisini Oluştur
                        string jobFolder = $"{parentJob.JobNumber}_{safeJobName}";
                        string equipFolder = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{safeEquipName}";
                        string partFolder = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{newPart.PartId}_{safePartName}";

                        string targetDirectory = Path.Combine(baseAttachmentPath, jobFolder, equipFolder, partFolder);

                        // F. Klasörü Oluştur (Eğer yoksa)
                        if (!Directory.Exists(targetDirectory))
                        {
                            Directory.CreateDirectory(targetDirectory);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Klasör oluşturma hatası işlemi durdurmasın, loglayıp devam etsin
                    System.Diagnostics.Debug.WriteLine($"Parça klasörü oluşturulamadı: {ex.Message}");
                }
                // ---------------------------------------------

                await transaction.CommitAsync();
                return newPart;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Eğer bu sınıfın içinde yoksa, bu yardımcı metodu da en alta ekleyin:
        private string SanitizeFolderName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unknown";
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
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

            string nextPartId = nextId.ToString();
            string nextPartCode = $"{equipCode}.{nextPartId}"; 

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