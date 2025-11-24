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
                    // Resmi DWG'nin olduğu yere (Sunucuya) kaydet
                    string imagesFolder = Path.GetDirectoryName(sourceDwgPath);
                    string targetThumbPath = Path.Combine(imagesFolder, targetThumbName);

                    // İlerleme Simülasyonu
                    var progressSimulator = Task.Run(async () =>
                    {
                        // IsProcessing false olana kadar veya %80'e gelene kadar
                        while (attachment.IsProcessing && attachment.ProcessingProgress < 0.8)
                        {
                            // DEĞİŞİKLİK: 100ms yerine 300ms bekletiyoruz.
                            await Task.Delay(300);

                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                // Daha büyük adımlarla ilerle
                                attachment.ProcessingProgress += 0.1;
                            });
                        }
                    });

                    // ASIL İŞLEM (Resim Oluşturma)
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
                            var options = new PngOptions { VectorRasterizationOptions = rasterizationOptions };
                            cadImage.Save(targetThumbPath, options);
                        }
                    });

                    // Veritabanı Güncelleme
                    var dbAttachment = await dbContext.EquipmentAttachments.FindAsync(attachment.Id);
                    if (dbAttachment != null)
                    {
                        dbAttachment.ThumbnailPath = targetThumbPath;
                        await dbContext.SaveChangesAsync();
                    }

                    // --- KRİTİK UI GÜNCELLEME SIRASI ---

                    // 1. Önce yolu ata (Hala "Yükleniyor" modundayız, bar görünür)
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        attachment.ProcessingProgress = 1.0;
                        attachment.ThumbnailPath = targetThumbPath; // Image kontrolü yüklemeye başlar
                    });

                    // 2. Kısa bir süre bekle (UI render etsin)
                    await Task.Delay(500);

                    // 3. Şimdi barı kapat, resim zaten arkada hazırdı, anında görünür.
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        attachment.IsProcessing = false;
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Thumbnail hatası: {ex.Message}");
                    MainThread.BeginInvokeOnMainThread(() => attachment.IsProcessing = false);
                }
            }
        }

        public async Task<EquipmentAttachment> AddAttachmentAsync(JobModel parentJob, Equipment parentEquipment, FileResult fileToCopy)
        {
            // 1. ADIM: Ortak Yolu (Veritabanından) Belirle
            string dbPath;
            try
            {
                // Veritabanından 'AttachmentPath' ayarını çek
                var setting = await _context.AppSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Key == "AttachmentPath");

                if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
                {
                    dbPath = setting.Value; // Veritabanındaki yol (Örn: \\SERVER\TrackerData)
                }
                else
                {
                    // Ayar yoksa varsayılan yerel belgelerim
                    dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase");
                }
            }
            catch
            {
                // Veritabanı hatası olursa varsayılan yol
                dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase");
            }

            // 2. ADIM: Ana Klasör Yolunu Oluştur
            // DÜZELTME BURADA: GetBaseDatabasePath() yerine 'dbPath' kullanıyoruz.
            string baseAttachmentPath = Path.Combine(dbPath, "Attachments");

            // Eğer ağdaki ana klasör (Attachments) yoksa oluşturmayı dene
            if (!Directory.Exists(baseAttachmentPath))
            {
                Directory.CreateDirectory(baseAttachmentPath);
            }

            // 3. ADIM: Klasör İsimlendirmeleri
            string safeJobName = SanitizeFolderName(parentJob.JobName);
            string safeEquipName = SanitizeFolderName(parentEquipment.Name);

            string jobFolder = $"{parentJob.JobNumber}_{safeJobName}";
            string equipFolder = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{safeEquipName}";

            // Hedef: ...\Attachments\İşNo_Adı\İşNo_EkipmanID_Adı
            string targetDirectory = Path.Combine(baseAttachmentPath, jobFolder, equipFolder);

            // Alt klasörleri oluştur
            if (!Directory.Exists(targetDirectory)) Directory.CreateDirectory(targetDirectory);

            // 4. ADIM: Dosyayı Kopyala
            string targetFilePath = Path.Combine(targetDirectory, fileToCopy.FileName);

            using (var sourceStream = await fileToCopy.OpenReadAsync())
            using (var targetStream = File.Create(targetFilePath))
            {
                await sourceStream.CopyToAsync(targetStream);
            }

            // 5. ADIM: Veritabanı Kaydı
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

            // 6. ADIM: DWG/DXF ise Küçük Resim İşlemini Başlat
            string extension = Path.GetExtension(targetFilePath).ToLower();
            if (extension == ".dwg" || extension == ".dxf")
            {
                string cleanFileName = Path.GetFileNameWithoutExtension(fileToCopy.FileName);
                string thumbName = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{cleanFileName}_thumb.png";

                // Arka plan işlemini başlat (Nesnenin kendisini gönderiyoruz)
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