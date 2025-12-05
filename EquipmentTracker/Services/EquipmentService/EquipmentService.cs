// Dosya: Services/Equipment/EquipmentService.cs
using EquipmentTracker.Data;
using EquipmentTracker.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace EquipmentTracker.Services.EquipmentService
{
    public class EquipmentService : IEquipmentService
    {
        private readonly DataContext _context;
        private readonly FtpHelper _ftpHelper;

        public EquipmentService(DataContext context, FtpHelper ftpHelper)
        {
            _context = context;
            _ftpHelper = ftpHelper;
        }
        private string GetCurrentAttachmentPath()
        {
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase");
            string basePath = Preferences.Get("attachment_path", defaultPath);
            return Path.Combine(basePath, "Attachments");
        }
        private string GetBaseDatabasePath()
        {
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase");
            return Preferences.Get("attachment_path", defaultPath);
        }
        public async Task<List<Equipment>> GetEquipmentsByJobIdAsync(int jobId)
        {
            return await _context.Equipments
                .Where(e => e.JobId == jobId)
                .Include(e => e.Parts)
                .Include(e => e.Attachments)
                .ToListAsync();
        }

        private static string SanitizeFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return "Unknown";

            string sanitizedName = Regex.Replace(folderName, @"[\\/:*?""<>| ]", "_");

            return Regex.Replace(sanitizedName, @"_+", "_").Trim('_');
        }

        // 2. Ana Metot (Ağ Yolu Entegrasyonu Yapıldı)
        public async Task<Equipment> AddEquipmentAsync(JobModel parentJob, Equipment newEquipment)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);

            try
            {
                // 1. Bu işe ait en son ekipman ID'sini bul (AYNI KALIYOR)
                var allEquipmentIds = await _context.Equipments
                    .Where(e => e.JobId == parentJob.Id)
                    .Select(e => e.EquipmentId)
                    .ToListAsync();

                int maxId = 0;
                foreach (var idStr in allEquipmentIds)
                {
                    if (int.TryParse(idStr, out int id))
                    {
                        if (id > maxId) maxId = id;
                    }
                }
                int nextId = maxId + 1;

                // 2. Yeni numaraları ata (AYNI KALIYOR)
                newEquipment.EquipmentId = nextId.ToString();
                newEquipment.EquipmentCode = $"{parentJob.JobNumber}.{nextId}";
                newEquipment.JobId = parentJob.Id;

                // 3. Veritabanına Kaydet (AYNI KALIYOR)
                _context.Equipments.Add(newEquipment);
                await _context.SaveChangesAsync();

                // Entity takibini bırak
                _context.Entry(newEquipment).State = EntityState.Detached;

                // --- 4. Klasör Oluşturma (GÜNCELLENDİ: Ortak Ağ Yolu) ---
                try
                {
                    // A. Ortak Yolu (Veritabanından) Çek
                    string dbPath;
                    var setting = await _context.AppSettings
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Key == "AttachmentPath");

                    if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
                        dbPath = setting.Value; // Veritabanındaki yol (Örn: \\SERVER\TrackerData)
                    else
                        dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase");

                    // B. Ana Klasör Yolu
                    string baseAttachmentPath = Path.Combine(dbPath, "Attachments");

                    // C. İsimleri Temizle
                    string safeJobName = SanitizeFolderName(parentJob.JobName);
                    string safeEquipName = SanitizeFolderName(newEquipment.Name);

                    string jobFolder = $"{parentJob.JobNumber}_{safeJobName}";
                    string equipFolder = $"{parentJob.JobNumber}_{newEquipment.EquipmentId}_{safeEquipName}";

                    string localTargetDirectory = Path.Combine(baseAttachmentPath, jobFolder, equipFolder);

                    if (!Directory.Exists(localTargetDirectory))
                    {
                        Directory.CreateDirectory(localTargetDirectory);
                    }
                    string ftpPath = $"Attachments/{jobFolder}/{equipFolder}";
                    await _ftpHelper.CreateDirectoryAsync(ftpPath);
                }
                catch (Exception ex)
                {
                    // Klasör hatası işlemi geri almasın, loglayıp devam etsin
                    System.Diagnostics.Debug.WriteLine($"Equipment klasör hatası: {ex.Message}");
                }
                // -----------------------------------------------------------

                await transaction.CommitAsync();
                return newEquipment;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
       
        
        public async Task ToggleEquipmentStatusAsync(int equipmentId, bool isCancelled)
        {
            var equipment = await _context.Equipments.FindAsync(equipmentId);
            if (equipment != null)
            {
                equipment.IsCancelled = isCancelled;
                await _context.SaveChangesAsync();
                _context.Entry(equipment).State = EntityState.Detached;
            }
        }

        public async Task<(string nextEquipId, string nextEquipCode)> GetNextEquipmentIdsAsync(JobModel parentJob)
        {
            string jobNum = parentJob.JobNumber;

            // 1. Bu işe ait tüm ekipman ID'lerini çek
            // (SQL tarafında string sıralaması "10", "2"den önce geldiği için hatalı çalışır,
            // bu yüzden hepsini çekip C# tarafında sayıya çevirip sıralayacağız.)
            var allEquipmentIds = await _context.Equipments
                .Where(e => e.JobId == parentJob.Id)
                .Select(e => e.EquipmentId)
                .ToListAsync();

            int maxId = 0;

            // 2. Bellekte integer'a çevirip en büyüğünü bul
            foreach (var idStr in allEquipmentIds)
            {
                if (int.TryParse(idStr, out int id))
                {
                    if (id > maxId) maxId = id;
                }
            }

            // 3. Bir fazlasını al
            int nextId = maxId + 1;

            string nextEquipId = nextId.ToString();
            string nextEquipCode = $"{jobNum}.{nextEquipId}";

            return (nextEquipId, nextEquipCode);
        }
        public async Task UpdateEquipmentNameAsync(Equipment equipment, string newName)
        {
            // 1. Değişiklik Yoksa Çık
            if (equipment.Name == newName) return;

            // Job bilgisini çek (Klasör yolları için gerekli)
            // Eğer equipment.Job null ise veritabanından çekelim
            var job = equipment.Job;
            if (job == null)
            {
                job = await _context.Jobs.FindAsync(equipment.JobId);
            }
            if (job == null) return;

            // 2. İsimleri ve Klasörleri Hazırla
            string safeJobName = SanitizeFolderName(job.JobName);
            string oldSafeEquipName = SanitizeFolderName(equipment.Name);
            string newSafeEquipName = SanitizeFolderName(newName);

            string jobFolder = $"{job.JobNumber}_{safeJobName}";

            string oldEquipFolder = $"{job.JobNumber}_{equipment.EquipmentId}_{oldSafeEquipName}";
            string newEquipFolder = $"{job.JobNumber}_{equipment.EquipmentId}_{newSafeEquipName}";

            // 3. Yerel Klasörü Taşı (Varsa)
            string baseAttachmentPath = GetBaseDatabasePath();
            string oldLocalPath = Path.Combine(baseAttachmentPath, "Attachments", jobFolder, oldEquipFolder);
            string newLocalPath = Path.Combine(baseAttachmentPath, "Attachments", jobFolder, newEquipFolder);

            // Yerel klasör varsa ismini değiştir
            if (Directory.Exists(oldLocalPath))
            {
                try
                {
                    if (!Directory.Exists(newLocalPath))
                        Directory.Move(oldLocalPath, newLocalPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Yerel Klasör Taşıma Hatası: {ex.Message}");
                }
            }
            // Yerel 'Images' klasörünü de taşı (Thumbnail'ler için)
            string oldLocalImgPath = Path.Combine(baseAttachmentPath, "Attachments", "Images", jobFolder, oldEquipFolder);
            string newLocalImgPath = Path.Combine(baseAttachmentPath, "Attachments", "Images", jobFolder, newEquipFolder);
            if (Directory.Exists(oldLocalImgPath))
            {
                try { Directory.Move(oldLocalImgPath, newLocalImgPath); } catch { }
            }


            // 4. FTP Klasörünü Taşı (Arka Planda)
            _ = Task.Run(async () =>
            {
                try
                {
                    // Ana Klasör: Attachments/Job/EskiEquip -> Attachments/Job/YeniEquip
                    string ftpOldPath = $"Attachments/{jobFolder}/{oldEquipFolder}";
                    string ftpNewPath = $"Attachments/{jobFolder}/{newEquipFolder}";
                    await _ftpHelper.RenameFileOrDirectoryAsync(ftpOldPath, ftpNewPath);

                    // Resim Klasörü: Attachments/Images/Job/EskiEquip -> ...
                    string ftpImagesOldPath = $"Attachments/Images/{jobFolder}/{oldEquipFolder}";
                    string ftpImagesNewPath = $"Attachments/Images/{jobFolder}/{newEquipFolder}";
                    await _ftpHelper.RenameFileOrDirectoryAsync(ftpImagesOldPath, ftpImagesNewPath);
                }
                catch (Exception fex)
                {
                    System.Diagnostics.Debug.WriteLine($"FTP Rename Error: {fex.Message}");
                }
            });

            // 5. Veritabanı Yollarını Güncelle (String Replace ile)
            // Ekipmana ait dosyaların yollarını güncelle
            var attachments = await _context.EquipmentAttachments
                .Where(a => a.EquipmentId == equipment.Id).ToListAsync();

            foreach (var att in attachments)
            {
                if (!string.IsNullOrEmpty(att.FilePath))
                    att.FilePath = att.FilePath.Replace(oldEquipFolder, newEquipFolder);

                if (!string.IsNullOrEmpty(att.ThumbnailPath))
                    att.ThumbnailPath = att.ThumbnailPath.Replace(oldEquipFolder, newEquipFolder);
            }

            // Ekipmanın Parçalarına ait dosyaların yollarını güncelle
            var parts = await _context.EquipmentParts
                .Where(p => p.EquipmentId == equipment.Id)
                .Include(p => p.Attachments)
                .ToListAsync();

            foreach (var part in parts)
            {
                foreach (var patt in part.Attachments)
                {
                    if (!string.IsNullOrEmpty(patt.FilePath))
                        patt.FilePath = patt.FilePath.Replace(oldEquipFolder, newEquipFolder);

                    if (!string.IsNullOrEmpty(patt.ThumbnailPath))
                        patt.ThumbnailPath = patt.ThumbnailPath.Replace(oldEquipFolder, newEquipFolder);
                }
            }

            // 6. İsmi Güncelle ve Kaydet
            equipment.Name = newName;

            // Eğer EquipmentCode isme bağlıysa onu da güncellemek gerekebilir ama genelde sabit kalır.
            // equipment.EquipmentCode = ... (Gerekirse buraya ekleyin)

            _context.Equipments.Update(equipment);
            await _context.SaveChangesAsync();
            _context.Entry(equipment).State = EntityState.Detached;
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