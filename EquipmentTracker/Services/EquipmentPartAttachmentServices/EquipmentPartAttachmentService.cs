// Dosya: Services/EquipmentPartAttachmentServices/EquipmentPartAttachmentService.cs
using EquipmentTracker.Data;
using EquipmentTracker.Models;
using Microsoft.EntityFrameworkCore;
using Aspose.CAD; 
using Aspose.CAD.ImageOptions; 
using System.Diagnostics;

namespace EquipmentTracker.Services.EquipmentPartAttachmentServices
{
    public class EquipmentPartAttachmentService : IEquipmentPartAttachmentService
    {
        private readonly DataContext _context;
        private readonly string _baseAttachmentPath;

        public EquipmentPartAttachmentService(DataContext context)
        {
            _context = context;

            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string appDataDirectory = Path.Combine(documentsPath, "TrackerDatabase");
            _baseAttachmentPath = Path.Combine(appDataDirectory, "Attachments");

            
            if (!Directory.Exists(_baseAttachmentPath))
            {
                Directory.CreateDirectory(_baseAttachmentPath);
            }
        }

        public async Task<EquipmentPartAttachment> AddAttachmentAsync(JobModel parentJob, Equipment parentEquipment, EquipmentPart parentPart, FileResult fileToCopy)
        {
            // 1. Hedef klasör yapısını oluştur (Aynı)
            string jobFolder = $"Job_{parentJob.JobNumber}";
            string equipFolder = $"Equip_{parentEquipment.EquipmentCode}";
            string partFolder = $"Part_{parentPart.PartCode}";
            string targetDirectory = Path.Combine(_baseAttachmentPath, jobFolder, equipFolder, partFolder);

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

            // 3. YENİ ADIM: Küçük Resim Oluşturma
            string thumbnailPath = null;
            string extension = Path.GetExtension(targetFilePath).ToLower();

            if (extension == ".dwg" || extension == ".dxf")
            {
                try
                {
                    // DÜZELTME: Bu ağır işlemi bir arka plan thread'ine gönder.
                    thumbnailPath = await Task.Run(() => GenerateCadThumbnail(targetFilePath));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Thumbnail oluşturulamadı: {ex.Message}");
                    thumbnailPath = null;
                }
            }

            // 4. Veritabanı nesnesini oluştur (ThumbnailPath'i ekleyerek)
            var newAttachment = new EquipmentPartAttachment
            {
                FileName = fileToCopy.FileName,
                FilePath = targetFilePath,
                ThumbnailPath = thumbnailPath, // Oluşturulan resmin yolunu ekle
                EquipmentPartId = parentPart.Id
            };

            // 5. Veritabanına kaydet (Aynı)
            _context.EquipmentPartAttachments.Add(newAttachment);
            await _context.SaveChangesAsync();
            _context.Entry(newAttachment).State = EntityState.Detached;

            return newAttachment;
        }

        // Aspose.CAD kullanarak bir DWG/DXF dosyasından PNG küçük resmi oluşturur.
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
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "Dosya silinirken bir hata oluştu: " + ex.Message, "Tamam");
            }
        }
    }
}