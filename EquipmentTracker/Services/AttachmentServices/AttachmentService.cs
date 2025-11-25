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

        private readonly FtpHelper _ftpHelper; // EKLENDİ

        public AttachmentService(DataContext context, IServiceProvider serviceProvider, FtpHelper ftpHelper)
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
        private async Task GenerateAndSaveThumbnailInBackground(EquipmentAttachment attachment, string sourceDwgPath, string targetThumbName, JobModel job, Equipment equip)
        {
            // UI: İşlem başladı bilgisini ver (Progress Bar görünür)
            attachment.IsProcessing = true;
            attachment.ProcessingProgress = 0.1;

            // Arka plan thread'inde veritabanı erişimi için yeni scope açıyoruz
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();

                try
                {
                    // A. YEREL İŞLEM: Thumbnail'i yerel diske, DWG'nin yanına kaydet
                    string imagesFolder = Path.GetDirectoryName(sourceDwgPath);
                    string localThumbPath = Path.Combine(imagesFolder, targetThumbName);

                    // İlerleme çubuğunu hareket ettirmek için simülasyon
                    var progressSimulator = Task.Run(async () =>
                    {
                        while (attachment.IsProcessing && attachment.ProcessingProgress < 0.8)
                        {
                            await Task.Delay(300);
                            MainThread.BeginInvokeOnMainThread(() => attachment.ProcessingProgress += 0.1);
                        }
                    });

                    // ASPOSE.CAD ile DWG -> PNG Çevirme İşlemi
                    await Task.Run(() =>
                    {
                        using (Aspose.CAD.Image cadImage = Aspose.CAD.Image.Load(sourceDwgPath))
                        {
                            var rasterizationOptions = new CadRasterizationOptions
                            {
                                PageWidth = 300, // Biraz daha kaliteli olsun diye 300 yaptım
                                PageHeight = 300,
                                Layouts = new[] { "Model" },
                                BackgroundColor = Aspose.CAD.Color.White,
                                DrawType = Aspose.CAD.FileFormats.Cad.CadDrawTypeMode.UseObjectColor
                            };
                            var options = new PngOptions { VectorRasterizationOptions = rasterizationOptions };
                            cadImage.Save(localThumbPath, options);
                        }
                    });

                    // B. VERİTABANI GÜNCELLEME (Yerel DB)
                    var dbAttachment = await dbContext.EquipmentAttachments.FindAsync(attachment.Id);
                    if (dbAttachment != null)
                    {
                        dbAttachment.ThumbnailPath = localThumbPath;
                        await dbContext.SaveChangesAsync();
                    }

                    // C. FTP YÜKLEME (Thumbnail'i de sunucuya atalım)
                    try
                    {
                        // FTP Klasör Yolunu Hesapla: Attachments/IsKlasoru/EkipmanKlasoru
                        string safeJobName = SanitizeFolderName(job.JobName);
                        string safeEquipName = SanitizeFolderName(equip.Name);
                        string jobFolder = $"{job.JobNumber}_{safeJobName}";
                        string equipFolder = $"{job.JobNumber}_{equip.EquipmentId}_{safeEquipName}";

                        string ftpFolderPath = $"Attachments/{jobFolder}/{equipFolder}";

                        // FtpHelper ile yükle
                        await _ftpHelper.UploadFileAsync(localThumbPath, ftpFolderPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Thumbnail FTP Hatası: {ex.Message}");
                    }

                    // D. UI GÜNCELLEME (İşlem Bitti)
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        attachment.ProcessingProgress = 1.0;
                        attachment.ThumbnailPath = localThumbPath; // Resmi ekranda göster
                    });

                    // Kısa bir bekleme süresi (kullanıcı %100'ü görsün)
                    await Task.Delay(500);

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        attachment.IsProcessing = false; // Barı gizle
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Thumbnail Oluşturma Hatası: {ex.Message}");
                    MainThread.BeginInvokeOnMainThread(() => attachment.IsProcessing = false);
                }
            }
        }

        public async Task<EquipmentAttachment> AddAttachmentAsync(JobModel parentJob, Equipment parentEquipment, FileResult fileToCopy)
        {
            // --- 1. ADIM: Yerel Klasör Yolunu Belirle ---
            string dbPath;
            try
            {
                var setting = await _context.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == "AttachmentPath");
                if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
                    dbPath = setting.Value;
                else
                    dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase");
            }
            catch
            {
                dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrackerDatabase");
            }

            string baseAttachmentPath = Path.Combine(dbPath, "Attachments");
            if (!Directory.Exists(baseAttachmentPath)) Directory.CreateDirectory(baseAttachmentPath);

            // --- 2. ADIM: Klasör İsimlendirmeleri ---
            string safeJobName = SanitizeFolderName(parentJob.JobName);
            string safeEquipName = SanitizeFolderName(parentEquipment.Name);

            string jobFolder = $"{parentJob.JobNumber}_{safeJobName}";
            string equipFolder = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{safeEquipName}";

            // Yerel Hedef: ...\Attachments\1_Is\1_1_Ekipman
            string targetDirectory = Path.Combine(baseAttachmentPath, jobFolder, equipFolder);

            if (!Directory.Exists(targetDirectory)) Directory.CreateDirectory(targetDirectory);

            // --- 3. ADIM: Dosyayı Yerele Kopyala ---
            string targetFilePath = Path.Combine(targetDirectory, fileToCopy.FileName);

            using (var sourceStream = await fileToCopy.OpenReadAsync())
            using (var targetStream = File.Create(targetFilePath))
            {
                await sourceStream.CopyToAsync(targetStream);
            }

            // --- 4. ADIM: Veritabanına Kaydet (Yerel DB) ---
            var newAttachment = new EquipmentAttachment
            {
                FileName = fileToCopy.FileName,
                FilePath = targetFilePath,
                ThumbnailPath = null,
                EquipmentId = parentEquipment.Id,
                IsProcessing = false,
                ProcessingProgress = 0
            };

            _context.EquipmentAttachments.Add(newAttachment);
            await _context.SaveChangesAsync();
            _context.Entry(newAttachment).State = EntityState.Detached;

            // --- 5. ADIM: FTP'ye Yükleme (Arka Planda) ---
            // Kullanıcı bekletilmez, işlem arkada devam eder.
            _ = Task.Run(async () =>
            {
                try
                {
                    string safeJobName = SanitizeFolderName(parentJob.JobName);
                    string safeEquipName = SanitizeFolderName(parentEquipment.Name);

                    string jobFolder = $"{parentJob.JobNumber}_{safeJobName}";
                    string equipFolder = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{safeEquipName}";

                    // FTP Klasör Yolu: Attachments/İş/Ekipman
                    string ftpFolderPath = $"Attachments/{jobFolder}/{equipFolder}";

                    // Klasörleri sırasıyla oluştur (Hata vermez, varsa geçer)
                    await _ftpHelper.CreateDirectoryAsync("Attachments");
                    await _ftpHelper.CreateDirectoryAsync($"Attachments/{jobFolder}");
                    await _ftpHelper.CreateDirectoryAsync(ftpFolderPath);

                    // ANA DOSYAYI YÜKLE
                    await _ftpHelper.UploadFileAsync(targetFilePath, ftpFolderPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"FTP Ana Dosya Yükleme Hatası: {ex.Message}");
                }
            });

            // --- 6. ADIM: DWG ise Thumbnail İşlemi ---
            string extension = Path.GetExtension(targetFilePath).ToLower();
            if (extension == ".dwg" || extension == ".dxf")
            {
                string cleanFileName = Path.GetFileNameWithoutExtension(fileToCopy.FileName);
                string thumbName = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{cleanFileName}_thumb.png";

                // Bu metodun içinde zaten FTP yüklemesi yapmıştık (Önceki cevabımda), o yüzden burası kalabilir.
                _ = Task.Run(() => GenerateAndSaveThumbnailInBackground(newAttachment, targetFilePath, thumbName, parentJob, parentEquipment));
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