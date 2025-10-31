using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EquipmentTracker.Models;


namespace EquipmentTracker.Services.Job
{
    public interface IJobService
    {
        // İleride API'ye bağlanacağımız için asenkron (async) tasarlıyoruz.
        Task<List<JobModel>> GetAllJobsAsync();
        Task<JobModel> GetJobByIdAsync(int jobId);
        Task<bool> UpdateJobAsync(JobModel job);
    }
}