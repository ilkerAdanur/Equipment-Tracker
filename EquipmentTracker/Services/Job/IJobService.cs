// Dosya: Services/Job/IJobService.cs
using EquipmentTracker.Models;

public interface IJobService
{
    Task<List<JobModel>> GetAllJobsAsync();
    Task<JobModel> GetJobByIdAsync(int jobId); // <-- YENİ
    Task AddJobAsync(JobModel newJob);
    Task<string> GetNextJobNumberAsync();
    Task<EquipmentPart> AddNewPartAsync(Equipment parentEquipment, EquipmentPart newPart);
}