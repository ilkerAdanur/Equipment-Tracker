using EquipmentTracker.Models;

namespace EquipmentTracker.Services.EquipmentPartAttachmentServices
{
    public interface IEquipmentPartAttachmentService
    {
        Task<EquipmentPartAttachment> AddAttachmentAsync(JobModel parentJob, EquipmentPart parentPart, FileResult fileToCopy);
        Task OpenAttachmentAsync(EquipmentPartAttachment attachment);
        Task DeleteAttachmentAsync(EquipmentPartAttachment attachment);
    }
}