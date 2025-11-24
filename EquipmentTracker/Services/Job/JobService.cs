// Dosya: Services/Job/JobService.cs
using EquipmentTracker.Data;
using EquipmentTracker.Models;
using EquipmentTracker.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace EquipmentTracker.Services.Job
{
    public class JobService : IJobService
    {
        private readonly DataContext _context;

        public JobService(DataContext context)
        {
            _context = context;


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
                name = name.Replace(c, '_');
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
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);

            try
            {
                // 1. İş Numarası Verme (AYNI)
                var allJobs = await _context.Jobs.Select(j => j.JobNumber).ToListAsync();
                int maxNumber = 0;
                if (allJobs.Any())
                {
                    foreach (var numStr in allJobs)
                    {
                        if (int.TryParse(numStr, out int num))
                        {
                            if (num > maxNumber) maxNumber = num;
                        }
                    }
                }
                newJob.JobNumber = (maxNumber + 1).ToString();

                // 2. Veritabanına Kayıt (AYNI)
                _context.Jobs.Add(newJob);
                await _context.SaveChangesAsync();

                // 3. KLASÖR OLUŞTURMA (DÜZELTİLDİ)
                try
                {
                    // BU METOT ZATEN ".../Attachments" DÖNDÜRÜYOR
                    string baseAttachmentPath = await GetGlobalAttachmentPathAsync();

                    // HATA BURADAYDI: Aşağıdaki satırı SİLDİK.
                    // string finalPath = Path.Combine(baseAttachmentPath, "Attachments"); <-- BU YANLIŞTI

                    string safeJobName = SanitizeFolderName(newJob.JobName);
                    string jobFolder = $"{newJob.JobNumber}_{safeJobName}";

                    // DOĞRUSU: Direkt gelen yolun altına iş klasörünü aç
                    string targetDirectory = Path.Combine(baseAttachmentPath, jobFolder);

                    if (!Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Job klasörü oluşturulamadı: {ex.Message}");
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
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
            // Klasör taşıma mantığı olan uzun metodunuz buraya gelecek (Değişiklik yok)
            // Önceki adımlarda yazdığımız UpdateJob metodu aynen kalabilir.
            var existingJob = await _context.Jobs.Include(j => j.Equipments).ThenInclude(e => e.Attachments).Include(j => j.Equipments).ThenInclude(e => e.Parts).ThenInclude(p => p.Attachments).FirstOrDefaultAsync(j => j.Id == jobId);

            if (existingJob != null)
            {
                string baseAttachmentPath = GetCurrentAttachmentPath();
                string oldSafeName = SanitizeFolderName(existingJob.JobName);
                string oldFolderName = $"{existingJob.JobNumber}_{oldSafeName}";
                string oldFullPath = Path.Combine(baseAttachmentPath, oldFolderName);

                existingJob.JobName = newJob.JobName;
                existingJob.JobOwner = newJob.JobOwner;
                existingJob.JobDescription = newJob.JobDescription;
                existingJob.Date = newJob.Date;

                existingJob.IsCancelled = newJob.IsCancelled;

                string newSafeName = SanitizeFolderName(newJob.JobName);
                string newFolderName = $"{existingJob.JobNumber}_{newSafeName}";
                string newFullPath = Path.Combine(baseAttachmentPath, newFolderName);

                if (!oldFullPath.Equals(newFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (Directory.Exists(oldFullPath))
                    {
                        try
                        {
                            if (!Directory.Exists(newFullPath))
                            {
                                Directory.Move(oldFullPath, newFullPath);
                                // Dosya yollarını güncelleme döngüleri buraya... (Önceki kodunuzdaki gibi)
                            }
                        }
                        catch (Exception ex) { Debug.WriteLine($"Klasör hatası: {ex.Message}"); }
                    }
                }

                try
                {
                    await _context.SaveChangesAsync();
                    _context.Entry(existingJob).State = EntityState.Detached;
                }
                catch (Exception ex) { Debug.WriteLine($"Update hatası: {ex.Message}"); throw; }
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