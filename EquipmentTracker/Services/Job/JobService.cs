// Dosya: Services/Job/JobService.cs
using EquipmentTracker.Data;
using EquipmentTracker.Models;
using EquipmentTracker.Models.Enums;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace EquipmentTracker.Services.Job
{
    public class JobService : IJobService
    {
        private readonly DataContext _context;
        private readonly FtpHelper _ftpHelper;

        public JobService(DataContext context, FtpHelper ftpHelper)
        {
            _context = context;
            _ftpHelper = ftpHelper;
        }

        public async Task<string> GetGlobalAttachmentPathAsync()
        {
            // 1. Varsayılan Yerel Yol (Yedek Plan)
            string localDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string defaultPath = Path.Combine(localDocuments, "TrackerDatabase", "Attachments");
            try
            {
                // 2. Veritabanından ayarı çek
                var setting = await _context.AppSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Key == "AttachmentPath");

                // 3. Eğer veritabanında geçerli bir yol varsa onu kullan
                if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
                {
                    return Path.Combine(setting.Value, "Attachments");
                }
            }
            catch (Exception ex)
            {
                // 4. Hata Yönetimi (Catch Dolu)
                Debug.WriteLine($"[GetGlobalAttachmentPathAsync] Kritik Hata: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"[GetGlobalAttachmentPathAsync] Detay: {ex.InnerException.Message}");
                }
            }
            // 5. Ayar bulunamadıysa veya Catch'e düştüyse varsayılanı döndür
            return defaultPath;
        }

        public async Task SetGlobalAttachmentPathAsync(string path)
        {
            var setting = await _context.AppSettings.FirstOrDefaultAsync(x => x.Key == "AttachmentPath");
            if (setting == null)
            {
                setting = new AppSetting { Key = "AttachmentPath", Value = path };
                _context.AppSettings.Add(setting);
            }
            else
            {
                setting.Value = path;
            }
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Ayarlardan mevcut yolu okur
        /// </summary>
        private string GetCurrentAttachmentPath()
        {
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase");
            string basePath = Preferences.Get("attachment_path", defaultPath);
            return Path.Combine(basePath, "Attachments");
        }

        /// <summary>
        /// Geçersiz klasör adı karakterlerini temizler.
        /// </summary>
        private string SanitizeFolderName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unknown";
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '.');
            }
            return name.Trim();
        }

        //private async Task SeedDataIfNeededAsync()
        //{
        //    if (await _context.Jobs.AnyAsync()) return;

        //    var job1 = new JobModel
        //    {
        //        JobNumber = "1",
        //        JobName = "AŞKALE ÇİMENTO PAKET ARITMASI",
        //        JobOwner = "STH ÇEVRE",
        //        Date = DateTime.Now,
        //        JobDescription = "Otomatik oluşturulan örnek veri.",
        //        MainApproval = ApprovalStatus.Pending
        //    };

        //    var job2 = new JobModel
        //    {
        //        JobNumber = "2",
        //        JobName = "TRABZON SU ARITMA TESİSİ",
        //        JobOwner = "TİSKİ",
        //        Date = DateTime.Now.AddDays(5),
        //        JobDescription = "Otomatik oluşturulan örnek veri.",
        //        MainApproval = ApprovalStatus.Approved
        //    };

        //    if (!await _context.Users.AnyAsync())
        //    {
        //        _context.Users.Add(new Users
        //        {
        //            Username = "admin",
        //            Password = "123",
        //            IsAdmin = true,
        //            FullName = "System Admin"
        //        });
        //        await _context.SaveChangesAsync();
        //    }

        //    await _context.Jobs.AddRangeAsync(job1, job2);
        //    await _context.SaveChangesAsync();
        //}

        private async Task SeedDataAsync()
        {
            // A. Varsayılan Admin Kullanıcısı Ekle
            if (!await _context.Users.AnyAsync())
            {
                var adminUser = new Users
                {
                    Username = "admin",
                    Password = "123", // Güçlü bir şifre belirleyebilirsiniz
                    FullName = "Sistem Yöneticisi",
                    IsAdmin = true,
                    IsOnline = false
                };
                _context.Users.Add(adminUser);
                await _context.SaveChangesAsync();
            }

            // B. Varsayılan Dosya Yolu Ayarını Ekle
            var pathSetting = await _context.AppSettings.FirstOrDefaultAsync(x => x.Key == "AttachmentPath");
            if (pathSetting == null)
            {
                // Varsayılan olarak sunucunun C diskinde bir klasör öneriyoruz (Admin sonra değiştirebilir)
                // Veya boş bırakıp Admin'in seçmesini bekleyebiliriz.
                _context.AppSettings.Add(new AppSetting
                {
                    Key = "AttachmentPath",
                    Value = @"C:\TrackerDatabase" // Başlangıç değeri
                });
                await _context.SaveChangesAsync();
            }
        }
        public async Task InitializeDatabaseAsync()
        {
            try
            {
                // 1. Veritabanı ve Tabloları Oluştur (Eğer yoksa)
                await _context.Database.EnsureCreatedAsync();

                // 2. Başlangıç Verilerini Doldur (Seed)
                await SeedDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Veritabanı başlatma hatası: {ex.Message}");
                throw;
            }
        }



        public async Task<List<JobModel>> GetAllJobsAsync()
        {
            var jobs = await _context.Jobs
                .AsNoTracking()
                .ToListAsync();

            return jobs.OrderBy(j => j.JobNumber.Length).ThenBy(j => j.JobNumber).ToList();
        }

        public async Task<JobModel> GetJobByIdAsync(int jobId)
        {
            return await _context.Jobs
                .Include(j => j.Equipments).ThenInclude(e => e.Parts).ThenInclude(p => p.Attachments)
                .Include(j => j.Equipments).ThenInclude(e => e.Attachments)
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == jobId);
        }

        public async Task<string> GetNextJobNumberAsync()
        {
            try
            {
                // Sadece JobNumber sütununu çek (Tüm tabloyu değil)
                var allJobNumbers = await _context.Jobs
                    .AsNoTracking()
                    .Select(j => j.JobNumber)
                    .ToListAsync();

                if (!allJobNumbers.Any()) return "1";

                int maxNumber = 0;
                foreach (var numStr in allJobNumbers)
                {
                    if (int.TryParse(numStr, out int num))
                    {
                        if (num > maxNumber) maxNumber = num;
                    }
                }
                return (maxNumber + 1).ToString();
            }
            catch
            {
                return "1";
            }
        }

        public async Task AddJobAsync(JobModel newJob)
        {
            int maxRetries = 3; // En fazla 3 kere dene
            int currentRetry = 0;
            bool success = false;

            while (currentRetry < maxRetries && !success)
            {
                // Transaction her denemede yeni oluşturulmalı
                using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);

                try
                {
                    // 1. En Son İş Numarasını Al
                    var lastJob = await _context.Jobs
                        .OrderByDescending(j => j.Id)
                        .FirstOrDefaultAsync();

                    int nextJobNumber = 1;
                    if (lastJob != null && int.TryParse(lastJob.JobNumber, out int lastNum))
                    {
                        nextJobNumber = lastNum + 1;
                    }

                    newJob.JobNumber = nextJobNumber.ToString();

                    // 2. Veritabanına Kayıt
                    _context.Jobs.Add(newJob);
                    await _context.SaveChangesAsync();

                    // 3. Klasör Oluşturma (Sadece ilk başarılı kayıtta çalışır)
                    try
                    {
                        string baseAttachmentPath = await GetGlobalAttachmentPathAsync();
                        string safeJobName = SanitizeFolderName(newJob.JobName);
                        string jobFolder = $"{newJob.JobNumber}_{safeJobName}";

                        string localTargetDirectory = Path.Combine(baseAttachmentPath, jobFolder);
                        if (!Directory.Exists(localTargetDirectory)) Directory.CreateDirectory(localTargetDirectory);

                        await _ftpHelper.CreateDirectoryAsync("Attachments");
                        await _ftpHelper.CreateDirectoryAsync($"Attachments/{jobFolder}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Job klasör hatası: {ex.Message}");
                    }

                    await transaction.CommitAsync();
                    success = true; // Döngüden çık
                }
                catch (DbUpdateException ex)
                {
                    // Eğer hata "Duplicate Entry" (Aynı kayıt var) ise
                    // MySQL Hata Kodu 1062: Duplicate entry
                    if (ex.InnerException is MySqlException sqlEx && sqlEx.Number == 1062)
                    {
                        await transaction.RollbackAsync();
                        currentRetry++;

                        // EF Core takibi temizle ki sonraki turda çakışmasın
                        _context.Entry(newJob).State = EntityState.Detached;

                        if (currentRetry >= maxRetries) throw; // Deneme hakkı bitti, hatayı fırlat

                        // Kısa bir süre bekle (100ms - 500ms arası rastgele)
                        await Task.Delay(new Random().Next(100, 500));
                    }
                    else
                    {
                        await transaction.RollbackAsync();
                        throw; // Başka bir hataysa direkt fırlat
                    }
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }


        public async Task SyncAllFilesFromFtpAsync()
        {
            if (App.CurrentUser == null || !App.CurrentUser.IsAdmin)
            {
                return; 
            }
            try
            {
                string basePath = await GetGlobalAttachmentPathAsync(); 

                // 1. Ekipman Dosyalarını Kontrol Et
                var equipmentAttachments = await _context.EquipmentAttachments
                    .Include(a => a.Equipment).ThenInclude(e => e.Job)
                    .AsNoTracking()
                    .ToListAsync();

                foreach (var att in equipmentAttachments)
                {
                    if (att.Equipment?.Job == null) continue;

                    // Klasör Yollarını Hesapla
                    string safeJobName = SanitizeFolderName(att.Equipment.Job.JobName);
                    string safeEquipName = SanitizeFolderName(att.Equipment.Name);

                    string jobFolder = $"{att.Equipment.Job.JobNumber}_{safeJobName}";
                    string equipFolder = $"{att.Equipment.Job.JobNumber}_{att.Equipment.EquipmentId}_{safeEquipName}";

                    // A. Ana Dosya
                    string localFilePath = att.FilePath; // Veritabanındaki yol (eğer tam yolsa)
                                                         // Not: Veritabanındaki yol farklı bir bilgisayara ait olabilir.
                                                         // Doğrusu: Yolu dinamik olarak yeniden oluşturmaktır.
                    string expectedLocalPath = Path.Combine(basePath, jobFolder, equipFolder, att.FileName);
                    string ftpFilePath = $"Attachments/{jobFolder}/{equipFolder}/{att.FileName}";

                    if (!File.Exists(expectedLocalPath))
                    {
                        await _ftpHelper.DownloadFileAsync(ftpFilePath, expectedLocalPath);
                    }

                    // B. Thumbnail (Varsa) - YENİ IMAGES KLASÖRÜNE GÖRE
                    if (!string.IsNullOrEmpty(att.ThumbnailPath))
                    {
                        string thumbName = Path.GetFileName(att.ThumbnailPath);
                        string expectedThumbPath = Path.Combine(basePath, "Images", jobFolder, equipFolder, thumbName);
                        string ftpThumbPath = $"Attachments/Images/{jobFolder}/{equipFolder}/{thumbName}";

                        if (!File.Exists(expectedThumbPath))
                        {
                            await _ftpHelper.DownloadFileAsync(ftpThumbPath, expectedThumbPath);
                        }
                    }
                }

                // 2. Parça Dosyalarını Kontrol Et (Benzer mantık)
                var partAttachments = await _context.EquipmentPartAttachments
                    .Include(pa => pa.EquipmentPart).ThenInclude(p => p.Equipment).ThenInclude(e => e.Job)
                    .AsNoTracking()
                    .ToListAsync();

                foreach (var patt in partAttachments)
                {
                    if (patt.EquipmentPart?.Equipment?.Job == null) continue;

                    var job = patt.EquipmentPart.Equipment.Job;
                    var equip = patt.EquipmentPart.Equipment;
                    var part = patt.EquipmentPart;

                    string safeJobName = SanitizeFolderName(job.JobName);
                    string safeEquipName = SanitizeFolderName(equip.Name);
                    string safePartName = SanitizeFolderName(part.Name);

                    string jobFolder = $"{job.JobNumber}_{safeJobName}";
                    string equipFolder = $"{job.JobNumber}_{equip.EquipmentId}_{safeEquipName}";
                    string partFolder = $"{job.JobNumber}_{equip.EquipmentId}_{part.PartId}_{safePartName}";

                    // A. Ana Dosya
                    string expectedLocalPath = Path.Combine(basePath, jobFolder, equipFolder, partFolder, patt.FileName);
                    string ftpFilePath = $"Attachments/{jobFolder}/{equipFolder}/{partFolder}/{patt.FileName}";

                    if (!File.Exists(expectedLocalPath))
                    {
                        await _ftpHelper.DownloadFileAsync(ftpFilePath, expectedLocalPath);
                    }

                    // B. Thumbnail - Images Klasörüne Göre
                    if (!string.IsNullOrEmpty(patt.ThumbnailPath))
                    {
                        string thumbName = Path.GetFileName(patt.ThumbnailPath);
                        string expectedThumbPath = Path.Combine(basePath, "Images", jobFolder, equipFolder, partFolder, thumbName);
                        string ftpThumbPath = $"Attachments/Images/{jobFolder}/{equipFolder}/{partFolder}/{thumbName}";

                        if (!File.Exists(expectedThumbPath))
                        {
                            await _ftpHelper.DownloadFileAsync(ftpThumbPath, expectedThumbPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Sync Error: {ex.Message}");
            }
        }

        public async Task DeleteJobAsync(int jobId)
        {
            var job = await _context.Jobs.FindAsync(jobId);
            if (job != null) { _context.Jobs.Remove(job); await _context.SaveChangesAsync(); }
        }

        public async Task UpdateJobApprovalAsync(int jobId, ApprovalStatus newStatus)
        {
            var job = await _context.Jobs.FindAsync(jobId);
            if (job != null) { job.MainApproval = newStatus; await _context.SaveChangesAsync(); _context.Entry(job).State = EntityState.Detached; }
        }

        public async Task UpdateJob(int jobId, JobModel newJob)
        {
            var existingJob = await _context.Jobs
                .Include(j => j.Equipments).ThenInclude(e => e.Attachments)
                .Include(j => j.Equipments).ThenInclude(e => e.Parts).ThenInclude(p => p.Attachments)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (existingJob != null)
            {
                // 1. İsim Değişikliği Kontrolü
                string baseAttachmentPath = await GetGlobalAttachmentPathAsync();

                string oldSafeName = SanitizeFolderName(existingJob.JobName);
                string oldFolderName = $"{existingJob.JobNumber}_{oldSafeName}";

                string newSafeName = SanitizeFolderName(newJob.JobName);
                string newFolderName = $"{existingJob.JobNumber}_{newSafeName}";

                bool nameChanged = !oldFolderName.Equals(newFolderName, StringComparison.OrdinalIgnoreCase);

                // 2. Veritabanı Güncelleme
                existingJob.JobName = newJob.JobName;
                existingJob.JobOwner = newJob.JobOwner;
                existingJob.JobDescription = newJob.JobDescription;
                existingJob.Date = newJob.Date;
                existingJob.IsCancelled = newJob.IsCancelled;

                if (nameChanged)
                {
                    // A. Yerel Klasörü Taşı
                    string oldFullPath = Path.Combine(baseAttachmentPath, oldFolderName);
                    string newFullPath = Path.Combine(baseAttachmentPath, newFolderName);

                    if (Directory.Exists(oldFullPath))
                    {
                        try
                        {
                            if (!Directory.Exists(newFullPath)) Directory.Move(oldFullPath, newFullPath);
                        }
                        catch (Exception ex) { Debug.WriteLine($"Local Move Error: {ex.Message}"); }
                    }

                    // B. FTP Klasörünü Taşı (YENİ)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // Ana Klasörü Taşı
                            await _ftpHelper.RenameFileOrDirectoryAsync($"Attachments/{oldFolderName}", $"Attachments/{newFolderName}");

                            // Images Klasörünü Taşı (Thumbnails için)
                            await _ftpHelper.RenameFileOrDirectoryAsync($"Attachments/Images/{oldFolderName}", $"Attachments/Images/{newFolderName}");
                        }
                        catch (Exception fex)
                        {
                            Debug.WriteLine($"FTP Rename Error: {fex.Message}");
                        }
                    });

                    // C. Veritabanındaki Yolları Güncelle (String Replace ile)
                    foreach (var eq in existingJob.Equipments)
                    {
                        foreach (var att in eq.Attachments)
                        {
                            if (!string.IsNullOrEmpty(att.FilePath)) att.FilePath = att.FilePath.Replace(oldFolderName, newFolderName);
                            if (!string.IsNullOrEmpty(att.ThumbnailPath)) att.ThumbnailPath = att.ThumbnailPath.Replace(oldFolderName, newFolderName);
                        }
                        foreach (var part in eq.Parts)
                        {
                            foreach (var patt in part.Attachments)
                            {
                                if (!string.IsNullOrEmpty(patt.FilePath)) patt.FilePath = patt.FilePath.Replace(oldFolderName, newFolderName);
                                if (!string.IsNullOrEmpty(patt.ThumbnailPath)) patt.ThumbnailPath = patt.ThumbnailPath.Replace(oldFolderName, newFolderName);
                            }
                        }
                    }
                }

                try
                {
                    await _context.SaveChangesAsync();
                    _context.Entry(existingJob).State = EntityState.Detached;
                }
                catch (Exception ex) { Debug.WriteLine($"Update Error: {ex.Message}"); throw; }
            }
        }

        public async Task ToggleJobStatusAsync(int jobId, bool isCancelled)
        {
            // Sadece ilgili işi buluyoruz
            var job = await _context.Jobs.FindAsync(jobId);

            if (job != null)
            {
                // Sadece durumu değiştiriyoruz
                job.IsCancelled = isCancelled;

                // Kaydediyoruz
                await _context.SaveChangesAsync();

                // Entity takibini bırakıyoruz
                _context.Entry(job).State = EntityState.Detached;
            }
        }


    }
}