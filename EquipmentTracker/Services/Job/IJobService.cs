// Dosya: Services/Job/IJobService.cs
using EquipmentTracker.Models;
using EquipmentTracker.Models.Enums;

namespace EquipmentTracker.Services.Job;

public interface IJobService
{
    Task<List<JobModel>> GetAllJobsAsync();
    Task<JobModel> GetJobByIdAsync(int jobId); 
    Task AddJobAsync(JobModel newJob);
    Task DeleteJobAsync(int jobId);
    Task<string> GetNextJobNumberAsync();
    Task UpdateJobApprovalAsync(int jobId, ApprovalStatus newStatus);
    Task InitializeDatabaseAsync();
}