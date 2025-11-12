// Dosya: Services/Job/JobService.cs
using EquipmentTracker.Data;
using EquipmentTracker.Models;
using EquipmentTracker.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace EquipmentTracker.Services.Job
{
    public class JobService : IJobService
    {
        private readonly DataContext _context;

        public JobService(DataContext context)
        {
            _context = context;


        }
        private async Task SeedDataIfNeededAsync()
        {
            // Veritabanında zaten hiç iş var mı? (Asenkron)
            if (await _context.Jobs.AnyAsync())
            {
                return; // Veritabanı dolu, seeding'e gerek yok.
            }

            // Veritabanı boş, sahte verilerimizi ekleyelim.
            var job1 = new JobModel
            {
                JobNumber = "001",
                JobName = "AŞKALE ÇİMENTO PAKET ARITMASI",
                JobOwner = "STH ÇEVRE",
                Date = new DateTime(2025, 9, 15),
                CreatorName = "İlker",
                CreatorRole = "Role",
                JobDescription = "Açıklama1",
                Equipments = new ObservableCollection<Equipment> { /* ... */ }
            };
            var job2 = new JobModel
            {
                JobNumber = "002",
                JobName = "TRABZON SU ARITMA TESİSİ",
                JobOwner = "TİSKİ",
                Date = new DateTime(2025, 10, 20),
                CreatorName = "İlker",
                CreatorRole = "Role",
                JobDescription = "Açıklama2",
                Equipments = new ObservableCollection<Equipment> { /* ... */ }
            };

            await _context.Jobs.AddRangeAsync(job1, job2);
            await _context.SaveChangesAsync(); // (Asenkron)
        }

        public async Task InitializeDatabaseAsync()
        {
            // Veritabanının oluşturulduğundan emin ol (asenkron olarak)
            await _context.Database.EnsureCreatedAsync();

            // Veri tohumlamasını asenkron olarak yap
            await SeedDataIfNeededAsync();
        }

        public async Task<List<JobModel>> GetAllJobsAsync()
        {
            return await _context.Jobs
                .OrderBy(j => j.JobNumber)
                .ToListAsync();
        }

        public async Task<JobModel> GetJobByIdAsync(int jobId)
        {
            return await _context.Jobs
        .Include(j => j.Equipments) // İş'in Ekipmanlarını yükle
            .ThenInclude(e => e.Parts) // O Ekipmanların Parçalarını yükle
                .ThenInclude(p => p.Attachments) // <-- YENİ EKLENDİ: O Parçaların Dosyalarını yükle
        .Include(j => j.Equipments) // İş'in Ekipmanlarını (tekrar) yükle
            .ThenInclude(e => e.Attachments) // O Ekipmanların Dosyalarını yükle
        .AsNoTracking()
        .FirstOrDefaultAsync(j => j.Id == jobId);
        }

        public async Task<string> GetNextJobNumberAsync()
        {
            var lastJob = await _context.Jobs
                                .OrderByDescending(j => j.JobNumber)
                                .FirstOrDefaultAsync();
            if (lastJob == null) return "001";
            if (int.TryParse(lastJob.JobNumber, out int lastNumber))
            {
                return (lastNumber + 1).ToString("D3");
            }
            return "001";
        }

        public async Task AddJobAsync(JobModel newJob)
        {
            _context.Jobs.Add(newJob);
            await _context.SaveChangesAsync();
        }


        public async Task DeleteJobAsync(int jobId)
        {
            var job = await _context.Jobs.FindAsync(jobId);
            if (job != null)
            {
                _context.Jobs.Remove(job);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateJobApprovalAsync(int jobId, ApprovalStatus newStatus)
        {
            // 'FindAsync' nesneyi "izlenen" (tracked) olarak getirir
            var jobToUpdate = await _context.Jobs.FindAsync(jobId);

            if (jobToUpdate != null)
            {
                // Sadece 1 alanı güncelle
                jobToUpdate.MainApproval = newStatus;
                // Değişiklikleri kaydet
                await _context.SaveChangesAsync();
                _context.Entry(jobToUpdate).State = EntityState.Detached;
            }
            // Bu yöntem, ViewModel'deki "izlenmeyen" (untracked)
            // CurrentJob nesnesine hiç dokunmadığı için çok daha güvenlidir.
        }
    }
}