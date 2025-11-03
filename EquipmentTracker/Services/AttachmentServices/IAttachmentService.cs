using EquipmentTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EquipmentTracker.Services.AttachmentServices
{
    public interface IAttachmentService
    {
        Task<EquipmentAttachment> AddAttachmentAsync(JobModel parentJob, Equipment parentEquipment, FileResult fileToCopy);
        Task OpenAttachmentAsync(EquipmentAttachment attachment);
        Task DeleteAttachmentAsync(EquipmentAttachment attachment);
    }
}
