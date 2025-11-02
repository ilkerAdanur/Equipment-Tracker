// Dosya: Services/Job/IJobService.cs
using EquipmentTracker.Models;

namespace EquipmentTracker.Services.Job;

public interface IJobService
{
    Task<List<JobModel>> GetAllJobsAsync();
    Task<JobModel> GetJobByIdAsync(int jobId); // <-- YENİ
    Task AddJobAsync(JobModel newJob);
    Task<string> GetNextJobNumberAsync();
}