using Aspose.CAD;
using Aspose.CAD.ImageOptions;
using EquipmentTracker.Data;
using EquipmentTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection; 
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace EquipmentTracker.Services.AttachmentServices
{
    public class AttachmentService : IAttachmentService
    {
        private readonly DataContext _context;
        private readonly IServiceProvider _serviceProvider; //  Arka plan işlemi için gerekli

        public AttachmentService(DataContext context, IServiceProvider serviceProvider)
        {
            _context = context;
            _serviceProvider = serviceProvider;
        }

        private string GetBaseDatabasePath()
        {
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase");
            return Preferences.Get("attachment_path", defaultPath);
        }

        // YENİ: Resimlerin kaydedileceği özel klasör yolu
        private string GetImagesFolderPath()
        {
            string basePath = GetBaseDatabasePath();
            string imagesPath = Path.Combine(basePath, "Images"); // TrackerDatabase/Images

            if (!Directory.Exists(imagesPath))
            {
                Directory.CreateDirectory(imagesPath);
            }
            return imagesPath;
        }

        private static string SanitizeFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return string.Empty;
            string sanitizedName = Regex.Replace(folderName, @"[\\/:*?""<>| ]", "_");
            sanitizedName = Regex.Replace(sanitizedName, @"_+", "_");
            return sanitizedName.Trim('_');
        }

        // YENİ: Arka planda çalışan Thumbnail oluşturucu
        private async Task GenerateAndSaveThumbnailInBackground(EquipmentAttachment attachment, string sourceDwgPath, string targetThumbName)
        {
            // 1. UI Başlangıç Ayarları
            attachment.IsProcessing = true;
            attachment.ProcessingProgress = 0.1;

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();

                try
                {
                    string imagesFolder = GetImagesFolderPath();
                    string targetThumbPath = Path.Combine(imagesFolder, targetThumbName);

                    // 2. İlerleme Simülasyonu (Kullanıcı dondu sanmasın diye)
                    var progressSimulator = Task.Run(async () =>
                    {
                        while (attachment.ProcessingProgress < 0.8 && attachment.IsProcessing)
                        {
                            await Task.Delay(100);
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                attachment.ProcessingProgress += 0.05;
                            });
                        }
                    });

                    // 3. ASIL AĞIR İŞLEM (Resim Oluşturma)
                    await Task.Run(() =>
                    {
                        using (Aspose.CAD.Image cadImage = Aspose.CAD.Image.Load(sourceDwgPath))
                        {
                            var rasterizationOptions = new CadRasterizationOptions
                            {
                                PageWidth = 150,
                                PageHeight = 150,
                                Layouts = new[] { "Model" },
                                BackgroundColor = Aspose.CAD.Color.White,
                                DrawType = Aspose.CAD.FileFormats.Cad.CadDrawTypeMode.UseObjectColor
                            };
                            var options = new PngOptions
                            {
                                VectorRasterizationOptions = rasterizationOptions
                            };
                            cadImage.Save(targetThumbPath, options);
                        }
                    });

                    // 4. Bitiş Animasyonu (%100 yap)
                    MainThread.BeginInvokeOnMainThread(() => attachment.ProcessingProgress = 1.0);

                    // Dosyanın diske tam yazılması ve UI'ın nefes alması için kısa bekleme
                    await Task.Delay(250);

                    // 5. Veritabanı Güncelleme
                    var dbAttachment = await dbContext.EquipmentAttachments.FindAsync(attachment.Id);
                    if (dbAttachment != null)
                    {
                        dbAttachment.ThumbnailPath = targetThumbPath;
                        await dbContext.SaveChangesAsync();
                    }

                    // 6. UI Güncelleme (Resmi Göster)
                    // Burada resmi atayıp işlem bayrağını indiriyoruz.
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        attachment.ThumbnailPath = targetThumbPath;
                        attachment.IsProcessing = false;
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Thumbnail hatası: {ex.Message}");
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        attachment.IsProcessing = false;
                    });
                }
            }
        }

        public async Task<EquipmentAttachment> AddAttachmentAsync(JobModel parentJob, Equipment parentEquipment, FileResult fileToCopy)
        {
            // 1. Klasör Yolları
            string baseAttachmentPath = Path.Combine(GetBaseDatabasePath(), "Attachments");
            string safeJobName = SanitizeFolderName(parentJob.JobName);
            string safeEquipName = SanitizeFolderName(parentEquipment.Name);
            string jobFolder = $"{parentJob.JobNumber}_{safeJobName}";
            string equipFolder = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{safeEquipName}";
            string targetDirectory = Path.Combine(baseAttachmentPath, jobFolder, equipFolder);

            if (!Directory.Exists(targetDirectory)) Directory.CreateDirectory(targetDirectory);

            // 2. Dosyayı Kopyala
            string targetFilePath = Path.Combine(targetDirectory, fileToCopy.FileName);
            using (var sourceStream = await fileToCopy.OpenReadAsync())
            using (var targetStream = File.Create(targetFilePath))
            {
                await sourceStream.CopyToAsync(targetStream);
            }

            var newAttachment = new EquipmentAttachment
            {
                FileName = fileToCopy.FileName,
                FilePath = targetFilePath,
                ThumbnailPath = null, // Henüz yok
                EquipmentId = parentEquipment.Id,
                IsProcessing = false,
                ProcessingProgress = 0
            };

            _context.EquipmentAttachments.Add(newAttachment);
            await _context.SaveChangesAsync();
            _context.Entry(newAttachment).State = EntityState.Detached;

            // GÜNCELLEME: İsimlendirme formatı ve yeni metoda çağrı
            string extension = Path.GetExtension(targetFilePath).ToLower();
            if (extension == ".dwg" || extension == ".dxf")
            {
                string cleanFileName = Path.GetFileNameWithoutExtension(fileToCopy.FileName);
                string thumbName = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{cleanFileName}_thumb.png";

                // DEĞİŞİKLİK BURADA: newAttachment nesnesinin kendisini gönderiyoruz.
                _ = Task.Run(() => GenerateAndSaveThumbnailInBackground(newAttachment, targetFilePath, thumbName));
            }

            return newAttachment;
        }

        public async Task OpenAttachmentAsync(EquipmentAttachment attachment)
        {
            if (attachment == null || string.IsNullOrEmpty(attachment.FilePath)) return;
            if (!File.Exists(attachment.FilePath))
            {
                await Shell.Current.DisplayAlert("Hata", "Dosya bulunamadı.", "Tamam");
                return;
            }
            await Launcher.Default.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(attachment.FilePath) });
        }

        public async Task DeleteAttachmentAsync(EquipmentAttachment attachment)
        {
            if (attachment == null) return;
            try
            {
                var entry = _context.EquipmentAttachments.Attach(attachment);
                entry.State = EntityState.Deleted;
                await _context.SaveChangesAsync();

                if (File.Exists(attachment.FilePath)) File.Delete(attachment.FilePath);

                // Küçük resmi de sil
                if (!string.IsNullOrEmpty(attachment.ThumbnailPath) && File.Exists(attachment.ThumbnailPath))
                {
                    File.Delete(attachment.ThumbnailPath);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "Dosya silinemedi: " + ex.Message, "Tamam");
            }
        }

        public async Task DeleteAttachmentRecordAsync(int attachmentId)
        {
            var attachment = await _context.EquipmentAttachments.FindAsync(attachmentId);
            if (attachment != null)
            {
                _context.EquipmentAttachments.Remove(attachment);
                await _context.SaveChangesAsync();
                _context.Entry(attachment).State = EntityState.Detached;
            }
        }
    }
}