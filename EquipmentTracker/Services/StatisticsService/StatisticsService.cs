// Dosya: Services/StatisticsServices/StatisticsService.cs
using EquipmentTracker.Data;
using EquipmentTracker.Models;
using EquipmentTracker.Models.Enums;
using EquipmentTracker.Services.StatisticsService;
using Microsoft.EntityFrameworkCore;

namespace EquipmentTracker.Services.StatisticsServices
{
    public class StatisticsService : IStatisticsService
    {
        private readonly DataContext _context;

        public StatisticsService(DataContext context)
        {
            _context = context;
        }

        public async Task<Statistics> GetDashboardStatisticsAsync()
        {
            // Toplam sayımlar (Hızlı sorgular)
            int totalJobs = await _context.Jobs.CountAsync();
            int totalEquipments = await _context.Equipments.CountAsync();
            int totalParts = await _context.EquipmentParts.CountAsync();
            int totalAttachments = await _context.EquipmentAttachments.CountAsync() +
                                   await _context.EquipmentPartAttachments.CountAsync();

            // Onay durumları
            int pendingJobs = await _context.Jobs.CountAsync(j => j.MainApproval == ApprovalStatus.Pending);
            int approvedJobs = await _context.Jobs.CountAsync(j => j.MainApproval == ApprovalStatus.Approved);
            int rejectedJobs = await _context.Jobs.CountAsync(j => j.MainApproval == ApprovalStatus.Rejected);

            // En fazla ekipmana sahip iş (İstediğiniz)
            var jobWithMostEquipment = await _context.Jobs
                .AsNoTracking()
                .OrderByDescending(j => j.Equipments.Count)
                .Select(j => j.JobName)
                .FirstOrDefaultAsync();

            // En fazla parçaya sahip iş (En karmaşık sorgu)
            var jobWithMostParts = await _context.Jobs
                .AsNoTracking()
                .Select(j => new
                {
                    JobName = j.JobName,
                    PartCount = j.Equipments.SelectMany(e => e.Parts).Count()
                })
                .OrderByDescending(x => x.PartCount)
                .Select(x => x.JobName)
                .FirstOrDefaultAsync();

            // Modeli doldur
            var stats = new Statistics
            {
                TotalJobs = totalJobs,
                TotalEquipments = totalEquipments,
                TotalParts = totalParts,
                TotalAttachments = totalAttachments,
                PendingJobs = pendingJobs,
                ApprovedJobs = approvedJobs,
                RejectedJobs = rejectedJobs,
                JobWithMostEquipment = jobWithMostEquipment ?? "N/A",
                JobWithMostParts = jobWithMostParts ?? "N/A"
            };

            return stats;
        }
    }
}