using EquipmentTracker.Data;
using EquipmentTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EquipmentTracker.Services.EquipmentPartAttachmentServices
{
    public class EquipmentPartAttachmentService : IEquipmentPartAttachmentService
    {
        private readonly DataContext _context;
        public EquipmentPartAttachmentService(DataContext context)
        {
            _context = context;
            // Ana 'Attachments' klasörünün var olduğundan emin ol
            if (!Directory.Exists(_baseAttachmentPath))
            {
                Directory.CreateDirectory(_baseAttachmentPath);
            }
        }
        private readonly string _baseAttachmentPath = Path.Combine(@"C:\TrackerDatabase", "Attachments");

        public Task<EquipmentPartAttachment> AddAttachmentAsync(JobModel parentJob, EquipmentPart parentPart, FileResult fileToCopy)
        {
            throw new NotImplementedException();
        }

        public Task OpenAttachmentAsync(EquipmentPartAttachment attachment)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAttachmentAsync(EquipmentPartAttachment attachment)
        {
            throw new NotImplementedException();
        }
    }
}
