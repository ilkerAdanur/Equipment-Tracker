using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WorkflowManagement.Models;

namespace WorkflowManagement.Services.Job
{

    public class JobService : IJobService
    {
        private readonly List<Models.Job.ResultJobModel> _jobs;

        public JobService()
        {
            _jobs = new List<Models.Job.ResultJobModel>
            {
                new Models.Job.ResultJobModel
                {
                    Id = 1,
                    Title = "İlk Müşteri Anlaşması",
                    ClientName = "Ahmet Yılmaz",
                    AgreementPrice = 15000,
                    ClientApproval = ResultApprovalStatusModel.Pending,
                    CreatedDate = DateTime.Now.AddDays(-2)
                },
                new Models.Job.ResultJobModel
                {
                    Id = 2,
                    Title = "Büyük Ofis Projesi",
                    ClientName = "Zeynep Kaya",
                    AgreementPrice = 120000,
                    ClientApproval = ResultApprovalStatusModel.Approved, 
                    MaterialsApproval = ResultApprovalStatusModel.Pending, 
                    Materials = new List<ResultMaterialModel>
                    {
                        new ResultMaterialModel { Id = 1, Name = "Ofis Masası", Quantity = 10, PricePerUnit = 800 },
                        new ResultMaterialModel { Id = 2, Name = "Yönetici Koltuğu", Quantity = 1, PricePerUnit = 2500 }
                    },
                    CreatedDate = DateTime.Now.AddDays(-1)
                },
                new Models.Job.ResultJobModel
                {
                    Id = 3,
                    Title = "Mağaza Tadilatı",
                    ClientName = "Mehmet Demir",
                    AgreementPrice = 45000,
                    ClientApproval = ResultApprovalStatusModel.Rejected, 
                    CreatedDate = DateTime.Now.AddDays(-3)
                }
            };
        }

        public Task<List<Models.Job.ResultJobModel>> GetAllJobsAsync()
        {
            return Task.FromResult(_jobs);
        }

        public Task<Models.Job.ResultJobModel> GetJobByIdAsync(int jobId)
        {
            var job = _jobs.FirstOrDefault(j => j.Id == jobId);
            return Task.FromResult(job);
        }

        public Task<bool> UpdateJobAsync(Models.Job.ResultJobModel jobToUpdate)
        {
            var existingJob = _jobs.FirstOrDefault(j => j.Id == jobToUpdate.Id);
            if (existingJob != null)
            {
                int index = _jobs.IndexOf(existingJob);
                _jobs[index] = jobToUpdate;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }
}