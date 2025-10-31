// Dosya: Services/Job/JobService.cs
using EquipmentTracker.Data;
using EquipmentTracker.Models;
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

            // Veritabanı ve tabloların var olduğundan emin ol.
            _context.Database.EnsureCreated();

            // Veritabanını SADECE boşsa doldur
            SeedDataIfNeeded();
        }

        // 1. TÜM İŞLERİ ÇEK (JobListPage için)
        public async Task<List<JobModel>> GetAllJobsAsync()
        {
            // Sadece ana iş listesini al, detaylara (ekipman) gerek yok
            return await _context.Jobs
                .OrderBy(j => j.JobNumber)
                .ToListAsync();
        }

        // 2. TEK BİR İŞİ DETAYLI ÇEK (JobDetailsPage için)
        public async Task<JobModel> GetJobByIdAsync(int jobId)
        {
            // Hiyerarşiyi (İş -> Ekipmanlar -> Parçalar) tam olarak yükle
            return await _context.Jobs
                .Include(j => j.Equipments)
                .ThenInclude(e => e.Parts)
                .FirstOrDefaultAsync(j => j.Id == jobId);
        }

        // 3. YENİ İŞ NUMARASINI HESAPLA
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

        // 4. YENİ İŞ EKLE
        public async Task AddJobAsync(JobModel newJob)
        {
            _context.Jobs.Add(newJob);
            await _context.SaveChangesAsync();
        }

        // 5. YENİ PARÇA EKLE
        public async Task<EquipmentPart> AddNewPartAsync(Equipment parentEquipment, EquipmentPart newPart)
        {
            newPart.EquipmentId = parentEquipment.Id;
            _context.EquipmentParts.Add(newPart);
            await _context.SaveChangesAsync();
            return newPart;
        }

        // 6. VERİTABANINI DOLDUR (Seed)
        private void SeedDataIfNeeded()
        {
            // Veritabanında zaten hiç iş var mı?
            if (_context.Jobs.Any())
            {
                return; // Veritabanı dolu, seeding'e gerek yok.
            }

            // Veritabanı boş, sahte verilerimizi ekleyelim.
            var job1 = new JobModel { /* ... AŞKALE verileri ... */ };
            var job2 = new JobModel { /* ... TRABZON verileri ... */ };

            // (Önceki mesajdaki SeedDataIfNeeded metodunun içini buraya kopyalayın)
            // Örnek:
            job1 = new JobModel
            {
                JobNumber = "001",
                JobName = "AŞKALE ÇİMENTO PAKET ARITMASI",
                JobOwner = "STH ÇEVRE",
                Date = new DateTime(2025, 9, 15),
                Equipments = new ObservableCollection<Equipment> { /* ... */ }
            };
            job2 = new JobModel
            {
                JobNumber = "002",
                JobName = "TRABZON SU ARITMA TESİSİ",
                JobOwner = "TİSKİ",
                Date = new DateTime(2025, 10, 20),
                Equipments = new ObservableCollection<Equipment> { /* ... */ }
            };

            _context.Jobs.AddRange(job1, job2);
            _context.SaveChanges();
        }
    }
}