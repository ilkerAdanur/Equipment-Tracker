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
            // 1. UI Başlangıç
            attachment.IsProcessing = true;
            attachment.ProcessingProgress = 0.1;

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                try
                {
                    // --- A. YEREL KLASÖR YAPISINI AYARLA ---

                    string dbPath = GetBaseDatabasePath();
                    string baseImagesPath = Path.Combine(dbPath, "Attachments", "Images");

                    string safeJobName = SanitizeFolderName(job.JobName);
                    string safeEquipName = SanitizeFolderName(equip.Name);
                    string safePartName = SanitizeFolderName(part.Name);

                    string jobFolder = $"{job.JobNumber}_{safeJobName}";
                    string equipFolder = $"{job.JobNumber}_{equip.EquipmentId}_{safeEquipName}";
                    string partFolder = $"{job.JobNumber}_{equip.EquipmentId}_{part.PartId}_{safePartName}";

                    // Hedef: .../Attachments/Images/Job/Equip/Part
                    string localImageDir = Path.Combine(baseImagesPath, jobFolder, equipFolder, partFolder);

                    if (!Directory.Exists(localImageDir))
                    {
                        Directory.CreateDirectory(localImageDir);
                    }

                    string targetThumbPath = Path.Combine(localImageDir, targetThumbName);

                    // --- B. İLERLEME SİMÜLASYONU ---
                    var progressSimulator = Task.Run(async () =>
                    {
                        while (attachment.ProcessingProgress < 0.8 && attachment.IsProcessing)
                        {
                            await Task.Delay(300);
                            MainThread.BeginInvokeOnMainThread(() => { attachment.ProcessingProgress += 0.05; });
                        }
                    });

                    // --- C. ASPOSE İLE RESİM OLUŞTURMA ---
                    await Task.Run(() =>
                    {
                        using (Aspose.CAD.Image cadImage = Aspose.CAD.Image.Load(sourceDwgPath))
                        {
                            var rasterizationOptions = new CadRasterizationOptions
                            {
                                PageWidth = 300,
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
                    var dbAttachment = await dbContext.EquipmentPartAttachments.FindAsync(attachment.Id);
                    if (dbAttachment != null)
                    {
                        dbAttachment.ThumbnailPath = targetThumbPath;
                        await dbContext.SaveChangesAsync();
                    }

                    // --- E. FTP'YE YÜKLEME (IMAGES KLASÖRÜNE) ---
                    try
                    {
                        // FTP Yollarını hazırla
                        string ftpImagesBase = "Attachments/Images";
                        string ftpJobImages = $"{ftpImagesBase}/{jobFolder}";
                        string ftpEquipImages = $"{ftpJobImages}/{equipFolder}";
                        string ftpTargetFolder = $"{ftpEquipImages}/{partFolder}";

                        // Klasörleri sırayla oluştur
                        await _ftpHelper.CreateDirectoryAsync("Attachments");
                        await _ftpHelper.CreateDirectoryAsync(ftpImagesBase);
                        await _ftpHelper.CreateDirectoryAsync(ftpJobImages);
                        await _ftpHelper.CreateDirectoryAsync(ftpEquipImages);
                        await _ftpHelper.CreateDirectoryAsync(ftpTargetFolder);

                        // Dosyayı yükle
                        await _ftpHelper.UploadFileAsync(targetThumbPath, ftpTargetFolder);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Part FTP Thumbnail Upload Error: {ex.Message}");
                    }

                    // --- F. UI GÜNCELLEME ---
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        attachment.ProcessingProgress = 1.0;
                        attachment.ThumbnailPath = targetThumbPath;
                    });

                    await Task.Delay(500);

                    MainThread.BeginInvokeOnMainThread(() => attachment.IsProcessing = false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Part Thumbnail Hatası: {ex.Message}");
                    MainThread.BeginInvokeOnMainThread(() => attachment.IsProcessing = false);
                }
            }
        }

        public async Task<EquipmentPartAttachment> UpdateAttachmentAsync(EquipmentPartAttachment existingAttachment, JobModel parentJob, Equipment parentEquipment, EquipmentPart parentPart, FileResult newFile)
        {
            // 1. Klasörler
            string dbPath = GetBaseDatabasePath();
            string baseAttachmentPath = Path.Combine(dbPath, "Attachments");

            string safeJobName = SanitizeFolderName(parentJob.JobName);
            string safeEquipName = SanitizeFolderName(parentEquipment.Name);
            string safePartName = SanitizeFolderName(parentPart.Name);

            string jobFolder = $"{parentJob.JobNumber}_{safeJobName}";
            string equipFolder = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{safeEquipName}";
            string partFolder = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{parentPart.PartId}_{safePartName}";

            string targetDirectory = Path.Combine(baseAttachmentPath, jobFolder, equipFolder, partFolder);
            if (!Directory.Exists(targetDirectory)) Directory.CreateDirectory(targetDirectory);

            // 2. Eski Dosya Temizliği
            string oldLocalPath = "";
            if (!string.IsNullOrEmpty(existingAttachment.FilePath))
            {
                string relativePath = existingAttachment.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString());
                oldLocalPath = Path.Combine(dbPath, relativePath);
            }

            if (existingAttachment.FileName != newFile.FileName && File.Exists(oldLocalPath))
            {
                try { File.Delete(oldLocalPath); } catch { }
            }

            if (!string.IsNullOrEmpty(existingAttachment.ThumbnailPath) && File.Exists(existingAttachment.ThumbnailPath))
            {
                try { File.Delete(existingAttachment.ThumbnailPath); } catch { }
            }

            // 3. Yeni Dosya (KİLİTLENME KONTROLÜ)
            string finalFilePath;
            string finalFileName;

            if (existingAttachment.FileName == newFile.FileName)
            {
                finalFilePath = Path.Combine(targetDirectory, newFile.FileName);
                finalFileName = newFile.FileName;

                if (File.Exists(finalFilePath))
                {
                    try
                    {
                        File.Delete(finalFilePath);
                    }
                    catch (IOException)
                    {
                        throw new Exception($"'{finalFileName}' dosyası şu anda kullanımda. Lütfen kapatıp tekrar deneyin.");
                    }
                }
            }
            else
            {
                finalFilePath = GetUniqueFilePath(targetDirectory, newFile.FileName);
                finalFileName = Path.GetFileName(finalFilePath);
            }

            using (var stream = await newFile.OpenReadAsync())
            using (var dest = File.Create(finalFilePath)) { await stream.CopyToAsync(dest); }

            // 4. Veritabanı
            string ftpRelativePath = $"Attachments/{jobFolder}/{equipFolder}/{partFolder}/{finalFileName}";

            existingAttachment.FileName = finalFileName;
            existingAttachment.FilePath = ftpRelativePath;
            existingAttachment.ThumbnailPath = null;
            existingAttachment.IsProcessing = true;
            existingAttachment.ProcessingProgress = 0;

            try
            {
                _context.EquipmentPartAttachments.Update(existingAttachment);
                await _context.SaveChangesAsync();
                _context.Entry(existingAttachment).State = EntityState.Detached;

                // 5. Arka Plan
                _ = Task.Run(() => ProcessPartAttachmentInBackground(existingAttachment, finalFilePath, ftpRelativePath, parentJob, parentEquipment, parentPart));
            }
            catch (Exception)
            {
                existingAttachment.IsProcessing = false;
                throw;
            }

            return existingAttachment;
        }

        public async Task<EquipmentPartAttachment> AddAttachmentAsync(JobModel parentJob, Equipment parentEquipment, EquipmentPart parentPart, FileResult fileToCopy)
        {
            string safeJobName = SanitizeFolderName(parentJob.JobName);
            string safeEquipName = SanitizeFolderName(parentEquipment.Name);
            string safePartName = SanitizeFolderName(parentPart.Name);

            string jobFolder = $"{parentJob.JobNumber}_{safeJobName}";
            string equipFolder = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{safeEquipName}";
            string partFolder = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{parentPart.PartId}_{safePartName}";

            string localTargetDir;
            if (App.CurrentUser?.IsAdmin ?? false) 
            {
                string dbPath = GetBaseDatabasePath();
                localTargetDir = Path.Combine(dbPath, "Attachments", jobFolder, equipFolder, partFolder);
            }
            else
            {
                localTargetDir = Path.Combine(FileSystem.CacheDirectory, "TempUploads");
            }

            if (!Directory.Exists(localTargetDir)) Directory.CreateDirectory(localTargetDir);

            string uniqueFilePath = GetUniqueFilePath(localTargetDir, fileToCopy.FileName);
            string uniqueFileName = Path.GetFileName(uniqueFilePath);

            using (var stream = await fileToCopy.OpenReadAsync())
            using (var dest = File.Create(uniqueFilePath)) { await stream.CopyToAsync(dest); }

            string ftpRelativePath = $"Attachments/{jobFolder}/{equipFolder}/{partFolder}/{uniqueFileName}";

            var newAttachment = new EquipmentPartAttachment
            {
                FileName = uniqueFileName,
                FilePath = ftpRelativePath, 
                ThumbnailPath = null,
                EquipmentPartId = parentPart.Id,
                IsProcessing = true
            };

            _context.EquipmentPartAttachments.Add(newAttachment);
            await _context.SaveChangesAsync();
            _context.Entry(newAttachment).State = EntityState.Detached;

            _ = Task.Run(() => ProcessPartAttachmentInBackground(newAttachment, uniqueFilePath, ftpRelativePath, parentJob, parentEquipment, parentPart));

            return newAttachment;
        }


        public async Task OpenAttachmentAsync(EquipmentPartAttachment attachment)
        {
            if (attachment == null || string.IsNullOrEmpty(attachment.FilePath)) return;
            if (!File.Exists(attachment.FilePath)) { await Shell.Current.DisplayAlert("Hata", "Dosya bulunamadı.", "Tamam"); return; }
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
                if (!string.IsNullOrEmpty(attachment.ThumbnailPath) && File.Exists(attachment.ThumbnailPath)) File.Delete(attachment.ThumbnailPath);
            }
            catch (Exception ex) { await Shell.Current.DisplayAlert("Hata", ex.Message, "Tamam"); }
        }

        public async Task DeletePartAttachmentRecordAsync(int attachmentId)
        {
            var attachment = await _context.EquipmentPartAttachments.FindAsync(attachmentId);
            if (attachment != null)
            {
                _context.EquipmentPartAttachments.Remove(attachment);
                await _context.SaveChangesAsync();
            }
        }

        private async Task ProcessPartAttachmentInBackground(EquipmentPartAttachment attachment, string sourceLocalPath, string ftpRelativePath, JobModel job, Equipment equip, EquipmentPart part)
        {
            if (job == null || equip == null || part == null) return;

            attachment.IsProcessing = true;
            attachment.ProcessingProgress = 0.0;

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                try
                {
                    // İsimleri Hazırla
                    string safeJobName = SanitizeFolderName(job.JobName);
                    string safeEquipName = SanitizeFolderName(equip.Name);
                    string safePartName = SanitizeFolderName(part.Name);

                    string jobFolder = $"{job.JobNumber}_{safeJobName}";
                    string equipFolder = $"{job.JobNumber}_{equip.EquipmentId}_{safeEquipName}";
                    string partFolder = $"{job.JobNumber}_{equip.EquipmentId}_{part.PartId}_{safePartName}";

                    // 1. FTP ANA DOSYA
                    string ftpFolderPath = Path.GetDirectoryName(ftpRelativePath).Replace("\\", "/");

                    var uploadProgress = new Progress<double>(percent =>
                    {
                        MainThread.BeginInvokeOnMainThread(() => attachment.ProcessingProgress = percent * 0.6);
                    });

                    // Klasörleri Sırayla Oluştur
                    await _ftpHelper.CreateDirectoryAsync("Attachments");
                    await _ftpHelper.CreateDirectoryAsync($"Attachments/{jobFolder}");
                    await _ftpHelper.CreateDirectoryAsync($"Attachments/{jobFolder}/{equipFolder}");
                    await _ftpHelper.CreateDirectoryAsync($"Attachments/{jobFolder}/{equipFolder}/{partFolder}");

                    await _ftpHelper.UploadFileWithProgressAsync(sourceLocalPath, ftpFolderPath, uploadProgress);

                    // 2. THUMBNAIL
                    string extension = Path.GetExtension(sourceLocalPath).ToLower();
                    if (extension == ".dwg" || extension == ".dxf")
                    {
                        MainThread.BeginInvokeOnMainThread(() => attachment.ProcessingProgress = 0.65);

                        string realThumbName = $"{Path.GetFileNameWithoutExtension(sourceLocalPath)}_thumb.png";

                        // FTP Resim Yolu: Attachments/Images/Job/Equip/Part
                        string ftpImagesBase = "Attachments/Images";
                        string ftpImagesPath = $"{ftpImagesBase}/{jobFolder}/{equipFolder}/{partFolder}";
                        string ftpThumbFullPath = $"{ftpImagesPath}/{realThumbName}";

                        // Geçici Dosya
                        string tempDir = Path.Combine(FileSystem.CacheDirectory, Guid.NewGuid().ToString());
                        if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
                        string safeTempPath = Path.Combine(tempDir, realThumbName);

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
                                cadImage.Save(safeTempPath, new PngOptions { VectorRasterizationOptions = rasterizationOptions });
                            }
                        });

                        var dbRecord = await dbContext.EquipmentPartAttachments.FindAsync(attachment.Id);
                        if (dbRecord != null)
                        {
                            dbRecord.ThumbnailPath = ftpThumbFullPath;
                            await dbContext.SaveChangesAsync();
                        }

                        MainThread.BeginInvokeOnMainThread(() => attachment.ThumbnailPath = safeTempPath);

                        MainThread.BeginInvokeOnMainThread(() => attachment.ProcessingProgress = 0.8);

                        await _ftpHelper.CreateDirectoryAsync("Attachments");
                        await _ftpHelper.CreateDirectoryAsync(ftpImagesBase);
                        await _ftpHelper.CreateDirectoryAsync($"{ftpImagesBase}/{jobFolder}");
                        await _ftpHelper.CreateDirectoryAsync($"{ftpImagesBase}/{jobFolder}/{equipFolder}");
                        await _ftpHelper.CreateDirectoryAsync(ftpImagesPath);

                        await _ftpHelper.UploadFileAsync(safeTempPath, ftpImagesPath);
                    }

                    bool isAdmin = App.CurrentUser?.IsAdmin ?? false;
                    if (!isAdmin && File.Exists(sourceLocalPath))
                    {
                        try { File.Delete(sourceLocalPath); } catch { }
                    }

                    MainThread.BeginInvokeOnMainThread(() => attachment.ProcessingProgress = 1.0);
                    await Task.Delay(300);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Part Arka Plan Hatası: {ex.Message}");
                }
                finally
                {
                    MainThread.BeginInvokeOnMainThread(() => attachment.IsProcessing = false);
                }
            }
        }


    }
}