using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WorkflowManagement.Models;

namespace WorkflowManagement.Services.Job
{

    public interface IJobService
    {
        // İleride API'ye bağlanacağımız için asenkron (async) tasarlıyoruz.
        Task<List<Models.Job.ResultJobModel>> GetAllJobsAsync();
        Task<Models.Job.ResultJobModel> GetJobByIdAsync(int jobId);
        Task<bool> UpdateJobAsync(Models.Job.ResultJobModel job);
    }
}