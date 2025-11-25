using Aspose.CAD;
using Aspose.CAD.ImageOptions;
using EquipmentTracker.Data;
using EquipmentTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace EquipmentTracker.Services.EquipmentPartAttachmentServices
{
    public class EquipmentPartAttachmentService : IEquipmentPartAttachmentService
    {
        private readonly DataContext _context;
        private readonly IServiceProvider _serviceProvider;
        private readonly FtpHelper _ftpHelper; // 1. FTP Helper Eklendi

        public EquipmentPartAttachmentService(DataContext context, IServiceProvider serviceProvider, FtpHelper ftpHelper)
        {
            _context = context;
            _serviceProvider = serviceProvider;
            _ftpHelper = ftpHelper;
        }

        private string GetBaseDatabasePath()
        {
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase");
            return Preferences.Get("attachment_path", defaultPath);
        }

        private string GetImagesFolderPath()
        {
            string basePath = GetBaseDatabasePath();
            string imagesPath = Path.Combine(basePath, "Images");
            if (!Directory.Exists(imagesPath)) Directory.CreateDirectory(imagesPath);
            return imagesPath;
        }

        private static string SanitizeFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return string.Empty;
            string sanitizedName = Regex.Replace(folderName, @"[\\/:*?""<>| ]", "_");
            sanitizedName = Regex.Replace(sanitizedName, @"_+", "_");
            return sanitizedName.Trim('_');
        }

        private async Task GenerateAndSaveThumbnailInBackground(EquipmentPartAttachment attachment, string sourceDwgPath, string targetThumbName, JobModel job, Equipment equip, EquipmentPart part)
        {
            attachment.IsProcessing = true;
            attachment.ProcessingProgress = 0.1;

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                try
                {
                    // 1. Küçük resmi yerel diske, DWG dosyasının yanına kaydet
                    string imagesFolder = Path.GetDirectoryName(sourceDwgPath);
                    string targetThumbPath = Path.Combine(imagesFolder, targetThumbName);

                    // İlerleme simülasyonu
                    var progressSimulator = Task.Run(async () =>
                    {
                        while (attachment.ProcessingProgress < 0.8 && attachment.IsProcessing)
                        {
                            await Task.Delay(300); // Biraz daha yavaş ilerlesin
                            MainThread.BeginInvokeOnMainThread(() => { attachment.ProcessingProgress += 0.05; });
                        }
                    });

                    // ASPOSE ile Çevirme İşlemi
                    await Task.Run(() =>
                    {
                        using (Aspose.CAD.Image cadImage = Aspose.CAD.Image.Load(sourceDwgPath))
                        {
                            var rasterizationOptions = new CadRasterizationOptions
                            {
                                PageWidth = 300, // Kalite için biraz büyüttük
                                PageHeight = 300,
                                Layouts = new[] { "Model" },
                                BackgroundColor = Aspose.CAD.Color.White,
                                DrawType = Aspose.CAD.FileFormats.Cad.CadDrawTypeMode.UseObjectColor
                            };
                            var options = new PngOptions { VectorRasterizationOptions = rasterizationOptions };
                            cadImage.Save(targetThumbPath, options);
                        }
                    });

                    // 2. Veritabanını Güncelle (Yerel yol ile)
                    var dbAttachment = await dbContext.EquipmentPartAttachments.FindAsync(attachment.Id);
                    if (dbAttachment != null)
                    {
                        dbAttachment.ThumbnailPath = targetThumbPath;
                        await dbContext.SaveChangesAsync();
                    }

                    // 3. FTP'ye Yükleme (YENİ KISIM)
                    try
                    {
                        // Klasör Yollarını Hazırla
                        string safeJobName = SanitizeFolderName(job.JobName);
                        string safeEquipName = SanitizeFolderName(equip.Name);
                        string safePartName = SanitizeFolderName(part.Name);

                        string jobFolder = $"{job.JobNumber}_{safeJobName}";
                        string equipFolder = $"{job.JobNumber}_{equip.EquipmentId}_{safeEquipName}";
                        string partFolder = $"{job.JobNumber}_{equip.EquipmentId}_{part.PartId}_{safePartName}";

                        // FTP Hedef Yolu: Attachments/Is/Ekipman/Parca
                        string ftpFolderPath = $"Attachments/{jobFolder}/{equipFolder}/{partFolder}";

                        // Oluşan küçük resmi sunucuya gönder
                        await _ftpHelper.UploadFileAsync(targetThumbPath, ftpFolderPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"FTP Thumbnail Upload Error: {ex.Message}");
                    }

                    // 4. UI Güncelleme
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        attachment.ProcessingProgress = 1.0;
                        attachment.ThumbnailPath = targetThumbPath;
                    });

                    await Task.Delay(500); // Kullanıcı %100'ü görsün

                    MainThread.BeginInvokeOnMainThread(() => attachment.IsProcessing = false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Part Thumbnail Hatası: {ex.Message}");
                    MainThread.BeginInvokeOnMainThread(() => attachment.IsProcessing = false);
                }
            }
        }

        public async Task<EquipmentPartAttachment> AddAttachmentAsync(JobModel parentJob, Equipment parentEquipment, EquipmentPart parentPart, FileResult fileToCopy)
        {
            // --- 1. ADIM: Ortak Yolu (Veritabanından) Belirle ---
            string dbPath;
            try
            {
                var setting = await _context.AppSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Key == "AttachmentPath");

                if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
                    dbPath = setting.Value;
                else
                    dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase");
            }
            catch
            {
                dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase");
            }

            // --- 2. ADIM: Klasör Yapısını Oluştur ---
            string baseAttachmentPath = Path.Combine(dbPath, "Attachments");

            if (!Directory.Exists(baseAttachmentPath))
                Directory.CreateDirectory(baseAttachmentPath);

            // İsimleri temizle
            string safeJobName = SanitizeFolderName(parentJob.JobName);
            string safeEquipName = SanitizeFolderName(parentEquipment.Name);
            string safePartName = SanitizeFolderName(parentPart.Name);

            // Klasör isimlerini oluştur
            string jobFolder = $"{parentJob.JobNumber}_{safeJobName}";
            string equipFolder = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{safeEquipName}";
            string partFolder = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{parentPart.PartId}_{safePartName}";

            // Tam Hedef Yolu: ...\Attachments\İş\Ekipman\Parça
            string targetDirectory = Path.Combine(baseAttachmentPath, jobFolder, equipFolder, partFolder);

            if (!Directory.Exists(targetDirectory)) Directory.CreateDirectory(targetDirectory);

            // --- 3. ADIM: Dosyayı Kopyala ---
            string targetFilePath = Path.Combine(targetDirectory, fileToCopy.FileName);

            using (var sourceStream = await fileToCopy.OpenReadAsync())
            using (var targetStream = File.Create(targetFilePath))
            {
                await sourceStream.CopyToAsync(targetStream);
            }

            // --- 4. ADIM: Veritabanı Kaydı ---
            var newAttachment = new EquipmentPartAttachment
            {
                FileName = fileToCopy.FileName,
                FilePath = targetFilePath,
                ThumbnailPath = null,
                EquipmentPartId = parentPart.Id,
                IsProcessing = false,
                ProcessingProgress = 0
            };

            _context.EquipmentPartAttachments.Add(newAttachment);
            await _context.SaveChangesAsync();
            _context.Entry(newAttachment).State = EntityState.Detached;

            // --- 5. ADIM: FTP'YE YÜKLEME (YENİ - ARKA PLANDA) ---
            // Kullanıcı arayüzünü dondurmadan FTP'ye yükle
            _ = Task.Run(async () =>
            {
                try
                {
                    // FTP Klasör Yolu
                    string ftpFolderPath = $"Attachments/{jobFolder}/{equipFolder}/{partFolder}";

                    // Klasör hiyerarşisini FTP'de oluştur (Sırayla)
                    await _ftpHelper.CreateDirectoryAsync("Attachments");
                    await _ftpHelper.CreateDirectoryAsync($"Attachments/{jobFolder}");
                    await _ftpHelper.CreateDirectoryAsync($"Attachments/{jobFolder}/{equipFolder}");
                    await _ftpHelper.CreateDirectoryAsync(ftpFolderPath);

                    // Dosyayı Yükle
                    await _ftpHelper.UploadFileAsync(targetFilePath, ftpFolderPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Part FTP Upload Error: {ex.Message}");
                }
            });

            // --- 6. ADIM: DWG/DXF ise Küçük Resim İşlemi ---
            string extension = Path.GetExtension(targetFilePath).ToLower();
            if (extension == ".dwg" || extension == ".dxf")
            {
                string cleanFileName = Path.GetFileNameWithoutExtension(fileToCopy.FileName);
                // Parça için benzersiz isim formatı
                string thumbName = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{parentPart.PartId}_{cleanFileName}_thumb.png";

                // Arka plan işlemini başlat (Parametreler GÜNCELLENDİ)
                _ = Task.Run(() => GenerateAndSaveThumbnailInBackground(newAttachment, targetFilePath, thumbName, parentJob, parentEquipment, parentPart));
            }

            return newAttachment;
        }

        public async Task OpenAttachmentAsync(EquipmentPartAttachment attachment)
        {
            if (attachment == null || string.IsNullOrEmpty(attachment.FilePath)) return;
            if (!File.Exists(attachment.FilePath))
            {
                await Shell.Current.DisplayAlert("Hata", "Dosya bulunamadı.", "Tamam");
                return;
            }
            await Launcher.Default.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(attachment.FilePath) });
        }

        public async Task DeleteAttachmentAsync(EquipmentPartAttachment attachment)
        {
            if (attachment == null) return;
            try
            {
                var entry = _context.EquipmentPartAttachments.Attach(attachment);
                entry.State = EntityState.Deleted;
                await _context.SaveChangesAsync();

                if (File.Exists(attachment.FilePath)) File.Delete(attachment.FilePath);
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

        public async Task DeletePartAttachmentRecordAsync(int attachmentId)
        {
            var attachment = await _context.EquipmentPartAttachments.FindAsync(attachmentId);
            if (attachment != null)
            {
                _context.EquipmentPartAttachments.Remove(attachment);
                await _context.SaveChangesAsync();
                _context.Entry(attachment).State = EntityState.Detached;
            }
        }
    }
}