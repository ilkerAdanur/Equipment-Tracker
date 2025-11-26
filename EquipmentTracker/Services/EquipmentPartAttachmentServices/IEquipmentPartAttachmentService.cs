using EquipmentTracker.Models;

namespace EquipmentTracker.Services.EquipmentPartAttachmentServices
{
    public interface IEquipmentPartAttachmentService
    {
        Task<EquipmentPartAttachment> AddAttachmentAsync(JobModel parentJob, Equipment parentEquipment, EquipmentPart parentPart, FileResult fileToCopy);
        Task OpenAttachmentAsync(EquipmentPartAttachment attachment);
        Task DeleteAttachmentAsync(EquipmentPartAttachment attachment);
        Task DeletePartAttachmentRecordAsync(int attachmentId);
        Task<EquipmentPartAttachment> UpdateAttachmentAsync(EquipmentPartAttachment existingAttachment, JobModel parentJob, Equipment parentEquipment, EquipmentPart parentPart, FileResult newFile);
    }
}