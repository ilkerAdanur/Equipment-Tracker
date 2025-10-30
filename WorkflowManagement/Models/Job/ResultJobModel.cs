using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WorkflowManagement.Models.ApprovalStatus;

namespace WorkflowManagement.Models.Job
{
    public class ResultJobModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public DateTime CreatedDate { get; set; } 

        // --- Adım 1: Müşteri Anlaşması ---
        public string ClientName { get; set; }
        public decimal AgreementPrice { get; set; }
        public string DocumentPath { get; set; } 
        public ResultApprovalStatusModel ClientApproval { get; set; } 

        // --- Adım 2: Mal Listesi (Eğer ClientApproval == Approved ise) ---
        public List<ResultMaterialModel> Materials { get; set; }
        public ResultApprovalStatusModel MaterialsApproval { get; set; } 

        // --- Adım 3: Kargo (Eğer MaterialsApproval == Approved ise) ---
        public string ShippingInfo { get; set; } 
        public bool IsShipped { get; set; }



        public ResultJobModel()
        {
            CreatedDate = DateTime.Now;
            ClientApproval = ResultApprovalStatusModel.Pending;
            MaterialsApproval = ResultApprovalStatusModel.Pending;
            Materials = new List<ResultMaterialModel>(); 
            IsShipped = false;
        }
    }
}
