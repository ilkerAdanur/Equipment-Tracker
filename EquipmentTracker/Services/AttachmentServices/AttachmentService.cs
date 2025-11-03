using EquipmentTracker.Data;
using EquipmentTracker.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EquipmentTracker.Services.AttachmentServices
{
    public class AttachmentService : IAttachmentService
    {
        private readonly DataContext _context;
        public AttachmentService(DataContext context)
        {
            _context = context;

            // 1. "Belgelerim" klasörünü bul
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            // 2. "Belgelerim\TrackerDatabase" yolunu oluştur
            string appDataDirectory = Path.Combine(documentsPath, "TrackerDatabase");
            // 3. Dosyaların ekleneceği ana klasör olarak "Belgelerim\TrackerDatabase\Attachments" yolunu ayarla
            _baseAttachmentPath = Path.Combine(appDataDirectory, "Attachments");

            // Ana 'Attachments' klasörünün var olduğundan emin ol
            if (!Directory.Exists(_baseAttachmentPath))
            {
                Directory.CreateDirectory(_baseAttachmentPath);
            }
        }
        private readonly string _baseAttachmentPath;

        public async Task<EquipmentAttachment> AddAttachmentAsync(JobModel parentJob, Equipment parentEquipment, FileResult fileToCopy)
        {
            // 1. Hedef klasör yapısını oluştur (örn: C:\...\Attachments\Job_003\Equip_003.001)
            string jobFolder = $"Job_{parentJob.JobNumber}";
            string equipFolder = $"Equip_{parentEquipment.EquipmentCode}";
            string targetDirectory = Path.Combine(_baseAttachmentPath, jobFolder, equipFolder);

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // 2. Dosyayı bu hedef klasöre kopyala
            string targetFilePath = Path.Combine(targetDirectory, fileToCopy.FileName);

            // Eğer aynı isimde dosya varsa, üzerine yaz (veya bir (1) ekleyebilirsiniz, şimdilik bu daha basit)
            using (var sourceStream = await fileToCopy.OpenReadAsync())
            using (var targetStream = File.Create(targetFilePath))
            {
                await sourceStream.CopyToAsync(targetStream);
            }

            // 3. Veritabanı nesnesini oluştur
            var newAttachment = new EquipmentAttachment
            {
                FileName = fileToCopy.FileName,
                FilePath = targetFilePath,
                EquipmentId = parentEquipment.Id
            };

            // 4. Veritabanına kaydet
            _context.EquipmentAttachments.Add(newAttachment);
            await _context.SaveChangesAsync();

            // 5. "İkişer ekleme" hatasını önlemek için izlemeyi bırak
            _context.Entry(newAttachment).State = EntityState.Detached;

            return newAttachment;
        }

        public async Task OpenAttachmentAsync(EquipmentAttachment attachment)
        {
            if (attachment == null || string.IsNullOrEmpty(attachment.FilePath))
            {
                await Shell.Current.DisplayAlert("Hata", "Dosya yolu bulunamadı.", "Tamam");
                return;
            }

            if (!File.Exists(attachment.FilePath))
            {
                await Shell.Current.DisplayAlert("Hata", "Dosya bulunamadı.\n\nDosya diskten silinmiş veya yeri değiştirilmiş olabilir.", "Tamam");
                return;
            }

            try
            {
                // Dosyayı sistemin varsayılan uygulamasıyla aç (PDF, DWG, TXT vb.)
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(attachment.FilePath)
                });
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "Dosya açılamadı: " + ex.Message, "Tamam");
            }
        }

        public async Task DeleteAttachmentAsync(EquipmentAttachment attachment)
        {
            if (attachment == null) return;

            try
            {
                // 1. Veritabanından sil
                var entry = _context.EquipmentAttachments.Attach(attachment);
                entry.State = EntityState.Deleted;
                await _context.SaveChangesAsync();

                // 2. Diskteki dosyayı sil
                if (File.Exists(attachment.FilePath))
                {
                    File.Delete(attachment.FilePath);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "Dosya silinirken bir hata oluştu: " + ex.Message, "Tamam");
            }
        }

    }
}
