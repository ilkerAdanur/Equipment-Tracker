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

        private string GetUniqueFilePath(string folderPath, string fileName)
        {
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            string fullPath = Path.Combine(folderPath, fileName);

            int counter = 1;
            while (File.Exists(fullPath))
            {
                string newFileName = $"{fileNameWithoutExt} ({counter}){extension}";
                fullPath = Path.Combine(folderPath, newFileName);
                counter++;
            }
            return fullPath;
        }


        private async Task ProcessAttachmentInBackground(EquipmentAttachment attachment, string sourceLocalPath, JobModel job, Equipment equip)
        {
            attachment.IsProcessing = true;
            attachment.ProcessingProgress = 0.0; // Başlangıç

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();

                try
                {
                    // Yolları Hazırla
                    string safeJobName = SanitizeFolderName(job.JobName);
                    string safeEquipName = SanitizeFolderName(equip.Name);
                    string jobFolder = $"{job.JobNumber}_{safeJobName}";
                    string equipFolder = $"{job.JobNumber}_{equip.EquipmentId}_{safeEquipName}";

                    string ftpFolderPath = $"Attachments/{jobFolder}/{equipFolder}";

                    // 1. FTP YÜKLEME (Ana Dosya) - %0'dan %60'a kadar
                    // İlerlemeyi yakalamak için Progress<double> kullanıyoruz
                    var uploadProgress = new Progress<double>(percent =>
                    {
                        // Yükleme işlemi toplam sürecin %60'ını kaplasın
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            attachment.ProcessingProgress = percent * 0.6;
                        });
                    });

                    // Klasörleri oluştur
                    await _ftpHelper.CreateDirectoryAsync("Attachments");
                    await _ftpHelper.CreateDirectoryAsync($"Attachments/{jobFolder}");
                    await _ftpHelper.CreateDirectoryAsync(ftpFolderPath);

                    // Yükle
                    await _ftpHelper.UploadFileWithProgressAsync(sourceLocalPath, ftpFolderPath, uploadProgress);

                    // 2. THUMBNAIL İŞLEMLERİ (Eğer DWG ise)
                    string extension = Path.GetExtension(sourceLocalPath).ToLower();
                    if (extension == ".dwg" || extension == ".dxf")
                    {
                        // UI Güncelleme: %60 -> "Resim Oluşturuluyor"
                        MainThread.BeginInvokeOnMainThread(() => attachment.ProcessingProgress = 0.65);

                        // A. Yerel Thumbnail Yolu (Images Klasörü)
                        string dbPath = GetBaseDatabasePath();
                        string baseImagesPath = Path.Combine(dbPath, "Attachments", "Images");
                        string localImageDir = Path.Combine(baseImagesPath, jobFolder, equipFolder);

                        if (!Directory.Exists(localImageDir)) Directory.CreateDirectory(localImageDir);

                        // Thumbnail ismi orijinal dosya ismiyle uyumlu olsun
                        string cleanFileName = Path.GetFileNameWithoutExtension(Path.GetFileName(sourceLocalPath));
                        string thumbName = $"{cleanFileName}_thumb.png";
                        string targetThumbPath = Path.Combine(localImageDir, thumbName);

                        // B. Resim Oluştur (Aspose)
                        await Task.Run(() =>
                        {
                            using (Aspose.CAD.Image cadImage = Aspose.CAD.Image.Load(sourceLocalPath))
                            {
                                var rasterizationOptions = new CadRasterizationOptions
                                {
                                    PageWidth = 300,
                                    PageHeight = 300,
                                    Layouts = new[] { "Model" },
                                    BackgroundColor = Aspose.CAD.Color.White,
                                    DrawType = Aspose.CAD.FileFormats.Cad.CadDrawTypeMode.UseObjectColor
                                };
                                cadImage.Save(targetThumbPath, new PngOptions { VectorRasterizationOptions = rasterizationOptions });
                            }
                        });

                        // C. Veritabanına Thumbnail Yolunu Yaz
                        var dbRecord = await dbContext.EquipmentAttachments.FindAsync(attachment.Id);
                        if (dbRecord != null)
                        {
                            dbRecord.ThumbnailPath = targetThumbPath;
                            await dbContext.SaveChangesAsync();
                        }

                        // UI Güncelleme: %80 -> "Resim Yükleniyor"
                        MainThread.BeginInvokeOnMainThread(() => attachment.ProcessingProgress = 0.8);

                        // D. Thumbnail'i FTP'ye Yükle (Images klasörüne)
                        string ftpImagesBase = "Attachments/Images";
                        string ftpThumbFolder = $"{ftpImagesBase}/{jobFolder}/{equipFolder}";

                        await _ftpHelper.CreateDirectoryAsync(ftpImagesBase);
                        await _ftpHelper.CreateDirectoryAsync($"{ftpImagesBase}/{jobFolder}");
                        await _ftpHelper.CreateDirectoryAsync(ftpThumbFolder);

                        await _ftpHelper.UploadFileAsync(targetThumbPath, ftpThumbFolder);

                        // UI: Resmi göster
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            attachment.ThumbnailPath = targetThumbPath;
                        });
                    }

                    // 3. BİTİŞ (%100)
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        attachment.ProcessingProgress = 1.0;
                    });

                    await Task.Delay(500); // Kullanıcı görsün diye az bekle
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Arka plan işlem hatası: {ex.Message}");
                }
                finally
                {
                    MainThread.BeginInvokeOnMainThread(() => attachment.IsProcessing = false);
                }
            }
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
            return sanitizedName.Replace("__", "_").Trim('_');
        }

        // YENİ: Arka planda çalışan Thumbnail oluşturucu
        private async Task GenerateAndSaveThumbnailInBackground(EquipmentAttachment attachment, string sourceDwgPath, string targetThumbName, JobModel job, Equipment equip)
        {
            // 1. UI Başlangıç Ayarları
            attachment.IsProcessing = true;
            attachment.ProcessingProgress = 0.1;

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();

                try
                {
                    // --- A. YEREL KLASÖR YAPISINI AYARLA (IMAGES KLASÖRÜ) ---

                    // 1. Temel yolları al
                    string dbPath = GetBaseDatabasePath();
                    string baseImagesPath = Path.Combine(dbPath, "Attachments", "Images");

                    // 2. Klasör isimlerini temizle
                    string safeJobName = SanitizeFolderName(job.JobName);
                    string safeEquipName = SanitizeFolderName(equip.Name);

                    string jobFolder = $"{job.JobNumber}_{safeJobName}";
                    string equipFolder = $"{job.JobNumber}_{equip.EquipmentId}_{safeEquipName}";

                    // 3. Hedef yerel klasör: .../Attachments/Images/Job/Equip
                    string localImageDir = Path.Combine(baseImagesPath, jobFolder, equipFolder);

                    // 4. Klasör yoksa oluştur
                    if (!Directory.Exists(localImageDir))
                    {
                        Directory.CreateDirectory(localImageDir);
                    }

                    // 5. Hedef dosya yolu
                    string targetThumbPath = Path.Combine(localImageDir, targetThumbName);

                    // --- B. İLERLEME SİMÜLASYONU ---
                    var progressSimulator = Task.Run(async () =>
                    {
                        while (attachment.IsProcessing && attachment.ProcessingProgress < 0.8)
                        {
                            await Task.Delay(300);
                            MainThread.BeginInvokeOnMainThread(() => attachment.ProcessingProgress += 0.1);
                        }
                    });

                    // --- C. ASPOSE İLE RESİM OLUŞTURMA ---
                    await Task.Run(() =>
                    {
                        using (Aspose.CAD.Image cadImage = Aspose.CAD.Image.Load(sourceDwgPath))
                        {
                            var rasterizationOptions = new CadRasterizationOptions
                            {
                                PageWidth = 300, // Kalite için 300px
                                PageHeight = 300,
                                Layouts = new[] { "Model" },
                                BackgroundColor = Aspose.CAD.Color.White,
                                DrawType = Aspose.CAD.FileFormats.Cad.CadDrawTypeMode.UseObjectColor
                            };
                            var options = new PngOptions { VectorRasterizationOptions = rasterizationOptions };
                            cadImage.Save(targetThumbPath, options);
                        }
                    });

                    // --- D. VERİTABANI GÜNCELLEME ---
                    var dbAttachment = await dbContext.EquipmentAttachments.FindAsync(attachment.Id);
                    if (dbAttachment != null)
                    {
                        dbAttachment.ThumbnailPath = targetThumbPath;
                        await dbContext.SaveChangesAsync();
                    }

                    // --- E. FTP'YE YÜKLEME (IMAGES KLASÖRÜNE) ---
                    try
                    {
                        // FTP Klasör Yolu: Attachments/Images/Job/Equip
                        string ftpImagesBase = "Attachments/Images";
                        string ftpJobImages = $"{ftpImagesBase}/{jobFolder}";
                        string ftpTargetFolder = $"{ftpJobImages}/{equipFolder}";

                        // Klasörleri sırayla oluştur (Garantiye almak için)
                        await _ftpHelper.CreateDirectoryAsync("Attachments");
                        await _ftpHelper.CreateDirectoryAsync(ftpImagesBase);
                        await _ftpHelper.CreateDirectoryAsync(ftpJobImages);
                        await _ftpHelper.CreateDirectoryAsync(ftpTargetFolder);

                        // Dosyayı yükle
                        await _ftpHelper.UploadFileAsync(targetThumbPath, ftpTargetFolder);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Thumbnail FTP Hatası: {ex.Message}");
                    }

                    // --- F. UI GÜNCELLEME ---
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        attachment.ProcessingProgress = 1.0;
                        attachment.ThumbnailPath = targetThumbPath;
                    });

                    await Task.Delay(500);

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
            // 1. Klasör Yollarını Hazırla
            string dbPath = GetBaseDatabasePath();
            string baseAttachmentPath = Path.Combine(dbPath, "Attachments");

            string safeJobName = SanitizeFolderName(parentJob.JobName);
            string safeEquipName = SanitizeFolderName(parentEquipment.Name);
            string jobFolder = $"{parentJob.JobNumber}_{safeJobName}";
            string equipFolder = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{safeEquipName}";

            string targetDirectory = Path.Combine(baseAttachmentPath, jobFolder, equipFolder);
            if (!Directory.Exists(targetDirectory)) Directory.CreateDirectory(targetDirectory);

            // 2. BENZERSİZ DOSYA ADI OLUŞTUR (Çakışma Önleme)
            string uniqueFilePath = GetUniqueFilePath(targetDirectory, fileToCopy.FileName);
            string uniqueFileName = Path.GetFileName(uniqueFilePath);

            // 3. Dosyayı Kopyala
            using (var sourceStream = await fileToCopy.OpenReadAsync())
            using (var targetStream = File.Create(uniqueFilePath))
            {
                await sourceStream.CopyToAsync(targetStream);
            }

            // 4. Veritabanı Kaydı
            var newAttachment = new EquipmentAttachment
            {
                FileName = uniqueFileName, // Yeni (benzersiz) isim
                FilePath = uniqueFilePath,
                ThumbnailPath = null,
                EquipmentId = parentEquipment.Id,
                IsProcessing = true, // Hemen işlem başlıyor diye işaretle
                ProcessingProgress = 0
            };

            _context.EquipmentAttachments.Add(newAttachment);
            await _context.SaveChangesAsync();
            _context.Entry(newAttachment).State = EntityState.Detached;

            // 5. ARKA PLAN İŞLEMİNİ BAŞLAT (Upload + Thumbnail)
            // Nesnenin kendisini gönderiyoruz, UI binding bu nesneye bağlı olduğu için Progress güncellemeleri ekrana yansıyacak.
            _ = Task.Run(() => ProcessAttachmentInBackground(newAttachment, uniqueFilePath, parentJob, parentEquipment));

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
                if (!string.IsNullOrEmpty(attachment.ThumbnailPath) && File.Exists(attachment.ThumbnailPath))
                {
                    File.Delete(attachment.ThumbnailPath);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", "Silme hatası: " + ex.Message, "Tamam");
            }
        }

        public async Task DeleteAttachmentRecordAsync(int attachmentId)
        {
            var attachment = await _context.EquipmentAttachments.FindAsync(attachmentId);
            if (attachment != null)
            {
                _context.EquipmentAttachments.Remove(attachment);
                await _context.SaveChangesAsync();
            }
        }

    }
}