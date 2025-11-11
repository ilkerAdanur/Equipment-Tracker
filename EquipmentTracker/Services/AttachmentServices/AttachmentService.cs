using Aspose.CAD; 
using Aspose.CAD.ImageOptions; 
using EquipmentTracker.Data;
using EquipmentTracker.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EquipmentTracker.Services.AttachmentServices
{
    public class AttachmentService : IAttachmentService
    {
        private readonly DataContext _context;
        private readonly string _baseAttachmentPath;

        public AttachmentService(DataContext context)
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

        public async Task<EquipmentAttachment> AddAttachmentAsync(JobModel parentJob, Equipment parentEquipment, FileResult fileToCopy)
        {
            // 1. Hedef klasör yapısını oluştur (Aynı)
            string jobFolder = $"Job_{parentJob.JobNumber}";
            string equipFolder = $"Equip_{parentEquipment.EquipmentCode}";
            string targetDirectory = Path.Combine(_baseAttachmentPath, jobFolder, equipFolder);

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // 2. Ana dosyayı (DWG, PDF vb.) kopyala (Aynı)
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
            var newAttachment = new EquipmentAttachment
            {
                FileName = fileToCopy.FileName,
                FilePath = targetFilePath,
                ThumbnailPath = thumbnailPath, // Oluşturulan resmin yolunu ekle
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

        /// <summary>
        /// Aspose.CAD kullanarak bir DWG/DXF dosyasından PNG küçük resmi oluşturur.
        /// </summary>
        private string GenerateCadThumbnail(string cadFilePath)
        {
            // 1. Çıktı PNG dosyasının yolunu belirle
            // Örn: ...\dosya.dwg -> ...\dosya_thumb.png
            string thumbFileName = $"{Path.GetFileNameWithoutExtension(cadFilePath)}_thumb.png";
            string thumbnailFilePath = Path.Combine(Path.GetDirectoryName(cadFilePath), thumbFileName);

            // 2. Aspose.CAD ile CAD dosyasını yükle
            using (Aspose.CAD.Image cadImage = Aspose.CAD.Image.Load(cadFilePath))
            {
                // 3. PNG'ye dönüştürme seçeneklerini ayarla
                var rasterizationOptions = new CadRasterizationOptions
                {
                    PageWidth = 150, // Küçük resim genişliği
                    PageHeight = 150, // Küçük resim yüksekliği
                    Layouts = new[] { "Model" } // "Model" alanını render et
                };

                var options = new PngOptions
                {
                    VectorRasterizationOptions = rasterizationOptions
                };

                // 4. PNG olarak kaydet
                cadImage.Save(thumbnailFilePath, options);
            }

            return thumbnailFilePath; // Yeni PNG'nin yolunu döndür
        }

    }
}
