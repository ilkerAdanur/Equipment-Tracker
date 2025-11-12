// Dosya: Services/EquipmentPartAttachmentServices/EquipmentPartAttachmentService.cs
using EquipmentTracker.Data;
using EquipmentTracker.Models;
using Microsoft.EntityFrameworkCore;
using Aspose.CAD; 
using Aspose.CAD.ImageOptions; 
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace EquipmentTracker.Services.EquipmentPartAttachmentServices
{
    public class EquipmentPartAttachmentService : IEquipmentPartAttachmentService
    {
        private readonly DataContext _context;

        public EquipmentPartAttachmentService(DataContext context)
        {
            _context = context;
        }

        /// <summary>
        /// YENİ YARDIMCI METOT: Geçersiz klasör adı karakterlerini temizler.
        /// </summary>
        private static string SanitizeFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
                return string.Empty;

            string sanitizedName = Regex.Replace(folderName, @"[\\/:*?""<>| ]", "_");
            sanitizedName = Regex.Replace(sanitizedName, @"_+", "_");
            return sanitizedName.Trim('_');
        }

        /// <summary>
        /// Ayarlardan mevcut yolu okur
        /// </summary>
        private string GetCurrentAttachmentPath()
        {
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase");
            string basePath = Preferences.Get("attachment_path", defaultPath);
            return Path.Combine(basePath, "Attachments");
        }

        private string GenerateCadThumbnail(string cadFilePath)
        {
            string thumbFileName = $"{Path.GetFileNameWithoutExtension(cadFilePath)}_thumb.png";
            string thumbnailFilePath = Path.Combine(Path.GetDirectoryName(cadFilePath), thumbFileName);
            using (Aspose.CAD.Image cadImage = Aspose.CAD.Image.Load(cadFilePath))
            {
                var rasterizationOptions = new CadRasterizationOptions { PageWidth = 150, PageHeight = 150, Layouts = new[] { "Model" } };
                var options = new PngOptions { VectorRasterizationOptions = rasterizationOptions };
                cadImage.Save(thumbnailFilePath, options);
            }
            return thumbnailFilePath;
        }


        public async Task<EquipmentPartAttachment> AddAttachmentAsync(JobModel parentJob, Equipment parentEquipment, EquipmentPart parentPart, FileResult fileToCopy)
        {
            // 1. GÜNCELLENDİ: Hedef klasör yapısını istediğiniz formata göre oluştur
            string baseAttachmentPath = GetCurrentAttachmentPath();

            // İsimleri temizle
            string safeJobName = SanitizeFolderName(parentJob.JobName);
            string safeEquipName = SanitizeFolderName(parentEquipment.Name);
            string safePartName = SanitizeFolderName(parentPart.Name);

            // Örn: 001_Aşkale
            string jobFolder = $"{parentJob.JobNumber}_{safeJobName}";

            // Örn: 001_001_Dozaj_Pompası
            string equipFolder = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{safeEquipName}";

            // Örn: 001_001_001_Yedek_Motor (PartId'nin 001 formatında olmasını varsayarak)
            string partFolder = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{parentPart.PartId}_{safePartName}";

            // Yolları birleştir
            string targetDirectory = Path.Combine(baseAttachmentPath, jobFolder, equipFolder, partFolder);

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // 2. Ana dosyayı kopyala (Aynı)
            string targetFilePath = Path.Combine(targetDirectory, fileToCopy.FileName);
            using (var sourceStream = await fileToCopy.OpenReadAsync())
            using (var targetStream = File.Create(targetFilePath))
            {
                await sourceStream.CopyToAsync(targetStream);
            }

            // 3. Küçük Resim Oluşturma (Aynı)
            string thumbnailPath = null;
            string extension = Path.GetExtension(targetFilePath).ToLower();

            if (extension == ".dwg" || extension == ".dxf")
            {
                try
                {
                    thumbnailPath = await Task.Run(() => GenerateCadThumbnail(targetFilePath));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Thumbnail oluşturulamadı: {ex.Message}");
                    thumbnailPath = null;
                }
            }

            // 4. Veritabanı nesnesini oluştur (Aynı)
            var newAttachment = new EquipmentPartAttachment
            {
                FileName = fileToCopy.FileName,
                FilePath = targetFilePath,
                ThumbnailPath = thumbnailPath,
                EquipmentPartId = parentPart.Id
            };

            // 5. Veritabanına kaydet (Aynı)
            _context.EquipmentPartAttachments.Add(newAttachment);
            await _context.SaveChangesAsync();
            _context.Entry(newAttachment).State = EntityState.Detached;

            return newAttachment;
        }

        // Aspose.CAD kullanarak bir DWG/DXF dosyasından PNG küçük resmi oluşturur.

      
        public async Task OpenAttachmentAsync(EquipmentPartAttachment attachment)
        {
            if (attachment == null || string.IsNullOrEmpty(attachment.FilePath))
            {
                await Shell.Current.DisplayAlert("Hata", "Dosya yolu bulunamadı.", "Tamam");
                return;
            }
            if (!File.Exists(attachment.FilePath))
            {
                await Shell.Current.DisplayAlert("Hata", "Dosya bulunamadı.", "Tamam");
                return;
            }
            try
            {
                await Launcher.Default.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(attachment.FilePath) });
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "Dosya açılamadı: " + ex.Message, "Tamam");
            }
        }

        public async Task DeleteAttachmentAsync(EquipmentPartAttachment attachment)
        {
            if (attachment == null) return;
            try
            {
                var entry = _context.EquipmentPartAttachments.Attach(attachment);
                entry.State = EntityState.Deleted;
                await _context.SaveChangesAsync();
                if (File.Exists(attachment.FilePath))
                {
                    File.Delete(attachment.FilePath);
                }
                // Küçük resmi de sil
                if (!string.IsNullOrEmpty(attachment.ThumbnailPath) && File.Exists(attachment.ThumbnailPath))
                {
                    File.Delete(attachment.ThumbnailPath);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "Dosya silinirken bir hata oluştu: " + ex.Message, "Tamam");
            }
        }


    }
}