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
        private static string SanitizeFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return string.Empty;
            string sanitizedName = Regex.Replace(folderName, @"[\\/:*?""<>| ]", "_");
            sanitizedName = Regex.Replace(sanitizedName, @"_+", "_");
            return sanitizedName.Trim('_');
        }

        private async Task SeedDataIfNeededAsync()
        {
            // Eğer Job tablosunda veri varsa çık, yoksa ekle.
            if (await _context.Jobs.AnyAsync()) return;

            var job1 = new JobModel
            {
                JobNumber = "1",
                JobName = "AŞKALE ÇİMENTO PAKET ARITMASI",
                JobOwner = "STH ÇEVRE",
                Date = DateTime.Now,
                JobDescription = "Otomatik oluşturulan örnek veri.",
                MainApproval = ApprovalStatus.Pending
            };

            var job2 = new JobModel
            {
                JobNumber = "2",
                JobName = "TRABZON SU ARITMA TESİSİ",
                JobOwner = "TİSKİ",
                Date = DateTime.Now.AddDays(5),
                JobDescription = "Otomatik oluşturulan örnek veri.",
                MainApproval = ApprovalStatus.Approved
            };

            await _context.Jobs.AddRangeAsync(job1, job2);
            await _context.SaveChangesAsync();
        }


        public async Task InitializeDatabaseAsync()
        {
            try
            {
                // 1. Veritabanı yoksa oluştur (Admin kullanıcısı ve Tablolar dahil)
                // Bu işlem SQL Server'daki 'dbUser' yetkilerini kullanır.
                await _context.Database.EnsureCreatedAsync();

                // 2. Ekstra başlangıç verileri (Varsa JobService içindeki seed)
                await SeedDataIfNeededAsync();
            }
            catch (Microsoft.Data.SqlClient.SqlException ex)
            {
                // Bağlantı hatası loglanır
                Debug.WriteLine($"SQL Hatası: {ex.Message}");
                // Burada UI'a bir sinyal gönderip "Ayarlar sayfasına git" denilebilir.
                throw; // Hatayı yukarı fırlat ki ViewModel yakalayabilsin
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Genel Veritabanı Hatası: {ex.Message}");
                throw;
            }
        }



        public async Task<List<JobModel>> GetAllJobsAsync()
        {
            var jobs = await _context.Jobs.ToListAsync();
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
            var allJobs = await _context.Jobs.Select(j => j.JobNumber).ToListAsync();
            if (!allJobs.Any()) return "1";
            int maxNumber = 0;
            foreach (var numStr in allJobs) { if (int.TryParse(numStr, out int num)) { if (num > maxNumber) maxNumber = num; } }
            return (maxNumber + 1).ToString();
        }

        public async Task AddJobAsync(JobModel newJob)
        {
            // Serializable izolasyon seviyesi, biz işlem yaparken araya başka kayıt girmesini engeller.
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                // 1. En güncel numarayı ŞU AN hesapla (Kilitli blok içinde)
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

                // Yeni numarayı ata (UI'dan gelen eski numarayı ezer)
                newJob.JobNumber = (maxNumber + 1).ToString();

                // 2. Kaydet
                _context.Jobs.Add(newJob);
                await _context.SaveChangesAsync();

                // 3. Klasör Oluşturma (İsim değişmiş olabileceği için yeni numarayla yapıyoruz)
                try
                {
                    string baseAttachmentPath = GetCurrentAttachmentPath();
                    string safeJobName = SanitizeFolderName(newJob.JobName);
                    string jobFolder = $"{newJob.JobNumber}_{safeJobName}";
                    string targetDirectory = Path.Combine(baseAttachmentPath, jobFolder);

                    if (!Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Job klasörü oluşturulamadı: {ex.Message}");
                }

                // 4. İşlemi Onayla
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