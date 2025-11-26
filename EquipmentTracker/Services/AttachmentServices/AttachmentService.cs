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

        public bool IsAdmin => App.CurrentUser?.IsAdmin ?? false;
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
                fullPath = Path.Combine(folderPath, $"{fileNameWithoutExt} ({counter}){extension}");
                counter++;
            }
            return fullPath;
        }


        private async Task ProcessAttachmentInBackground(EquipmentAttachment attachment, string sourceLocalPath, string ftpRelativePath, JobModel job, Equipment equip)
        {
            if (job == null || equip == null)
            {
                System.Diagnostics.Debug.WriteLine("HATA: Arka plan işleminde Job veya Equipment nesnesi NULL geldi.");
                return;
            }
            attachment.IsProcessing = true;
            attachment.ProcessingProgress = 0.0;


            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                try
                {
                    // FTP Klasör Yolları (Veritabanındaki yoldan çıkarılır)
                    string ftpFolderPath = Path.GetDirectoryName(ftpRelativePath).Replace("\\", "/");

                    // 1. FTP YÜKLEME
                    var uploadProgress = new Progress<double>(percent =>
                    {
                        MainThread.BeginInvokeOnMainThread(() => attachment.ProcessingProgress = percent * 0.6);
                    });

                    await _ftpHelper.CreateDirectoryAsync("Attachments");
                    // Alt klasörleri oluştur (Basit split mantığı)
                    var folders = ftpFolderPath.Split('/');
                    string currentPath = "";
                    foreach (var folder in folders)
                    {
                        if (string.IsNullOrEmpty(folder)) continue;
                        currentPath = string.IsNullOrEmpty(currentPath) ? folder : $"{currentPath}/{folder}";
                        await _ftpHelper.CreateDirectoryAsync(currentPath);
                    }

                    await _ftpHelper.UploadFileWithProgressAsync(sourceLocalPath, ftpFolderPath, uploadProgress);

                    // 2. THUMBNAIL (DWG ise)
                    string extension = Path.GetExtension(sourceLocalPath).ToLower();
                    if (extension == ".dwg" || extension == ".dxf")
                    {
                        MainThread.BeginInvokeOnMainThread(() => attachment.ProcessingProgress = 0.65);

                        // Thumbnail FTP Yolu: Attachments/Images/Job/Equip/file_thumb.png
                        string thumbName = $"{Path.GetFileNameWithoutExtension(sourceLocalPath)}_thumb.png";
                        string ftpImagesPath = $"Attachments/Images/{job.JobNumber}_{SanitizeFolderName(job.JobName)}/{job.JobNumber}_{equip.EquipmentId}_{SanitizeFolderName(equip.Name)}";
                        string ftpThumbFullPath = $"{ftpImagesPath}/{thumbName}";

                        // Thumbnail Oluşturma (Geçici bir yerde oluşturup yükleyelim)
                        string tempThumbPath = Path.Combine(FileSystem.CacheDirectory, thumbName);

                        await Task.Run(() =>
                        {
                            using (Aspose.CAD.Image cadImage = Aspose.CAD.Image.Load(sourceLocalPath))
                            {
                                var opts = new CadRasterizationOptions { PageWidth = 300, PageHeight = 300, Layouts = new[] { "Model" }, BackgroundColor = Aspose.CAD.Color.White, DrawType = Aspose.CAD.FileFormats.Cad.CadDrawTypeMode.UseObjectColor };
                                cadImage.Save(tempThumbPath, new PngOptions { VectorRasterizationOptions = opts });
                            }
                        });

                        // DB Güncelle (GÖRELİ YOL)
                        var dbRecord = await dbContext.EquipmentAttachments.FindAsync(attachment.Id);
                        if (dbRecord != null)
                        {
                            dbRecord.ThumbnailPath = ftpThumbFullPath; // Sadece sunucu yolu!
                            await dbContext.SaveChangesAsync();
                        }
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            // Modeli geçici yerel yolla güncelle
                            attachment.ThumbnailPath = tempThumbPath;
                        });

                        // FTP Yükle (Images)
                        MainThread.BeginInvokeOnMainThread(() => attachment.ProcessingProgress = 0.8);

                        // Images klasörlerini oluştur
                        await _ftpHelper.UploadFileAsync(tempThumbPath, ftpImagesPath);
                        string[] imgFolders = ftpImagesPath.Replace("Attachments/Images/", "").Split('/');
                        string currentImgPath = "Attachments/Images";
                        foreach (var f in imgFolders) { currentImgPath += $"/{f}"; await _ftpHelper.CreateDirectoryAsync(currentImgPath); }

                        await _ftpHelper.UploadFileAsync(tempThumbPath, ftpImagesPath);

                        // UI Güncelle (Geçici yerel yolu göster ki hemen görünsün)
                        MainThread.BeginInvokeOnMainThread(() => attachment.ThumbnailPath = tempThumbPath);
                    }

                    // Temizlik (Normal kullanıcıysa kaynak dosyayı cache'den sil)
                    if (!IsAdmin && File.Exists(sourceLocalPath))
                    {
                        File.Delete(sourceLocalPath);
                    }

                    MainThread.BeginInvokeOnMainThread(() => attachment.ProcessingProgress = 1.0);
                    await Task.Delay(500);
                }
                catch (Exception ex) { Debug.WriteLine($"Upload Error: {ex.Message}"); }
                finally { MainThread.BeginInvokeOnMainThread(() => attachment.IsProcessing = false); }
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

            // 2. İsim Çakışmasını Önle
            string uniqueFilePath = GetUniqueFilePath(targetDirectory, fileToCopy.FileName);
            string uniqueFileName = Path.GetFileName(uniqueFilePath);

            // 3. Dosyayı Kopyala
            using (var sourceStream = await fileToCopy.OpenReadAsync())
            using (var targetStream = File.Create(uniqueFilePath))
            {
                await sourceStream.CopyToAsync(targetStream);
            }

            // 4. Veritabanı Kaydı (FTP Yolu ile)
            string ftpRelativePath = $"Attachments/{jobFolder}/{equipFolder}/{uniqueFileName}";

            var newAttachment = new EquipmentAttachment
            {
                FileName = uniqueFileName,
                FilePath = ftpRelativePath,
                ThumbnailPath = null,
                EquipmentId = parentEquipment.Id,
                IsProcessing = true,
                ProcessingProgress = 0
            };

            _context.EquipmentAttachments.Add(newAttachment);
            await _context.SaveChangesAsync();
            _context.Entry(newAttachment).State = EntityState.Detached;

            // 5. Arka Plan İşlemini Başlat (DÜZELTİLDİ)
            // Artık ProcessAttachmentInBackground metodu 5 parametre bekliyor, biz de 5 tane gönderiyoruz.
            _ = Task.Run(() => ProcessAttachmentInBackground(newAttachment, uniqueFilePath, ftpRelativePath, parentJob, parentEquipment));

            return newAttachment;
        }

        public async Task<EquipmentAttachment> UpdateAttachmentAsync(EquipmentAttachment existingAttachment, JobModel parentJob, Equipment parentEquipment, FileResult newFile)
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

            // 2. Eski Yerel Dosyayı Sil (Sadece Adminde ve varsa)
            string oldLocalPath = "";
            if (!string.IsNullOrEmpty(existingAttachment.FilePath))
            {
                // DB'de "Attachments/..." şeklinde kayıtlı olabilir, onu tam yola çeviriyoruz
                string relativePath = existingAttachment.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString());
                oldLocalPath = Path.Combine(dbPath, relativePath);
            }
            else
            {
                oldLocalPath = existingAttachment.FilePath;
            }

            if (File.Exists(oldLocalPath))
            {
                try { File.Delete(oldLocalPath); } catch { }
            }

            // Eski Thumbnail'i de sil
            if (!string.IsNullOrEmpty(existingAttachment.ThumbnailPath) && File.Exists(existingAttachment.ThumbnailPath))
            {
                try { File.Delete(existingAttachment.ThumbnailPath); } catch { }
            }

            // 3. Yeni Dosyayı Kopyala
            string uniqueFilePath = GetUniqueFilePath(targetDirectory, newFile.FileName);
            string uniqueFileName = Path.GetFileName(uniqueFilePath);

            using (var sourceStream = await newFile.OpenReadAsync())
            using (var targetStream = File.Create(uniqueFilePath))
            {
                await sourceStream.CopyToAsync(targetStream);
            }

            // 4. Veritabanı Kaydını Güncelle
            string ftpRelativePath = $"Attachments/{jobFolder}/{equipFolder}/{uniqueFileName}";

            // Nesne özelliklerini güncelle
            existingAttachment.FileName = uniqueFileName;
            existingAttachment.FilePath = ftpRelativePath;
            existingAttachment.ThumbnailPath = null; // Yenisi oluşacak
            existingAttachment.IsProcessing = true;  // Progress bar görünsün
            existingAttachment.ProcessingProgress = 0;

            _context.EquipmentAttachments.Update(existingAttachment);
            await _context.SaveChangesAsync();
            _context.Entry(existingAttachment).State = EntityState.Detached;

            // 5. Arka Plan İşlemini Başlat (DÜZELTİLDİ)
            // ftpRelativePath parametresi eklendi
            _ = Task.Run(() => ProcessAttachmentInBackground(existingAttachment, uniqueFilePath, ftpRelativePath, parentJob, parentEquipment));

            return existingAttachment;
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
            // Sadece kaydı siliyoruz, fiziksel silme karmaşık olabilir (FTP'den silme vs.)
            // Şimdilik veritabanından silmek yeterli.
            var entry = _context.EquipmentAttachments.Attach(attachment);
            entry.State = EntityState.Deleted;
            await _context.SaveChangesAsync();

            // Eğer Admin ise yerel dosyayı da silsin
            if (IsAdmin)
            {
                string dbPath = GetBaseDatabasePath();
                // FilePath artık relative (Attachments/...) olduğu için başına kök dizini eklemiyoruz, combine ediyoruz.
                string localPath = Path.Combine(dbPath, attachment.FilePath.Replace("/", "\\"));
                if (File.Exists(localPath)) File.Delete(localPath);
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