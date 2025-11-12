using Aspose.CAD; 
using Aspose.CAD.ImageOptions; 
using EquipmentTracker.Data;
using EquipmentTracker.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace EquipmentTracker.Services.AttachmentServices
{
    public class AttachmentService : IAttachmentService
    {
        private readonly DataContext _context;

        public AttachmentService(DataContext context)
        {
            _context = context;
            //string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            //string appDataDirectory = Path.Combine(documentsPath, "TrackerDatabase");

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

        /// <summary>
        /// YENİ YARDIMCI METOT: Geçersiz klasör adı karakterlerini temizler.
        /// </summary>
        private static string SanitizeFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
                return string.Empty;

            // Geçersiz karakterleri ve boşlukları '_' ile değiştir
            string sanitizedName = Regex.Replace(folderName, @"[\\/:*?""<>| ]", "_");
            // Çoklu alt çizgileri teke indir
            sanitizedName = Regex.Replace(sanitizedName, @"_+", "_");
            // Başta veya sonda olabilecek alt çizgileri kaldır
            return sanitizedName.Trim('_');
        }

        private string GenerateCadThumbnail(string cadFilePath)
        {
            string thumbFileName = $"{Path.GetFileNameWithoutExtension(cadFilePath)}_thumb.png";
            string thumbnailFilePath = Path.Combine(Path.GetDirectoryName(cadFilePath), thumbFileName);
            using (Aspose.CAD.Image cadImage = Aspose.CAD.Image.Load(cadFilePath))
            {
                var rasterizationOptions = new CadRasterizationOptions
                {
                    PageWidth = 150,
                    PageHeight = 150,
                    Layouts = new[] { "Model" }
                };
                var options = new PngOptions
                {
                    VectorRasterizationOptions = rasterizationOptions
                };
                cadImage.Save(thumbnailFilePath, options);
            }
            return thumbnailFilePath;
        }

        public async Task<EquipmentAttachment> AddAttachmentAsync(JobModel parentJob, Equipment parentEquipment, FileResult fileToCopy)
        {
            // 1. GÜNCELLENDİ: Hedef klasör yapısını istediğiniz formata göre oluştur
            string baseAttachmentPath = GetCurrentAttachmentPath();

            // İsimleri temizle
            string safeJobName = SanitizeFolderName(parentJob.JobName);
            string safeEquipName = SanitizeFolderName(parentEquipment.Name);

            // İstediğiniz yeni klasör yapısı
            // Örn: 001_Aşkale
            string jobFolder = $"{parentJob.JobNumber}_{safeJobName}";

            // Örn: 001_001_Dozaj_Pompası
            string equipFolder = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{safeEquipName}";

            // Yolları birleştir
            string targetDirectory = Path.Combine(baseAttachmentPath, jobFolder, equipFolder);

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
            var newAttachment = new EquipmentAttachment
            {
                FileName = fileToCopy.FileName,
                FilePath = targetFilePath,
                ThumbnailPath = thumbnailPath,
                EquipmentId = parentEquipment.Id
            };

            // 5. Veritabanına kaydet (Aynı)
            _context.EquipmentAttachments.Add(newAttachment);
            await _context.SaveChangesAsync();
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
                await Shell.Current.DisplayAlert("Hata", "Dosya bulunamadı.", "Tamam");
                return;
            }
            try
            {
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
                var entry = _context.EquipmentAttachments.Attach(attachment);
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
