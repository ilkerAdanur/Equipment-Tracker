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
        private readonly IServiceProvider _serviceProvider;
        private readonly FtpHelper _ftpHelper;

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

        private static string SanitizeFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return string.Empty;
            string sanitizedName = Regex.Replace(folderName, @"[\\/:*?""<>| ]", "_");
            sanitizedName = Regex.Replace(sanitizedName, @"_+", "_");
            return sanitizedName.Trim('_');
        }

        // --- KRİTİK DÜZELTME: Thumbnail Oluşturma ve Kaydetme Mantığı ---
        private async Task ProcessAttachmentInBackground(EquipmentAttachment attachment, string sourceLocalPath, string ftpRelativePath, JobModel job, Equipment equip)
        {
            if (job == null || equip == null) return;

            // UI: İşlem Başladı
            attachment.IsProcessing = true;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                attachment.ProcessingProgress = 0.0;
                attachment.ThumbnailPath = null;
            });

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();

                // -- AŞAMA 1: ANA DOSYA YÜKLEME (DWG) --
                try
                {
                    string ftpFolderPath = Path.GetDirectoryName(ftpRelativePath).Replace("\\", "/");

                    var uploadProgress = new Progress<double>(percent =>
                    {
                        MainThread.BeginInvokeOnMainThread(() => attachment.ProcessingProgress = percent * 0.5);
                    });

                    // 1.1 Klasörleri oluştur (Parça servisiyle aynı yöntem)
                    var folders = ftpFolderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    string currentPath = "";
                    foreach (var folder in folders)
                    {
                        currentPath = string.IsNullOrEmpty(currentPath) ? folder : $"{currentPath}/{folder}";
                        await _ftpHelper.CreateDirectoryAsync(currentPath);
                    }

                    // 1.2 Ana dosyayı yükle
                    await _ftpHelper.UploadFileWithProgressAsync(sourceLocalPath, ftpFolderPath, uploadProgress);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ana Dosya Yükleme Hatası: {ex.Message}");
                }

                // -- AŞAMA 2: KÜÇÜK RESİM OLUŞTURMA (DWG/DXF) --
                string extension = Path.GetExtension(sourceLocalPath).ToLower();
                if (extension == ".dwg" || extension == ".dxf")
                {
                    string tempThumbPath = null;
                    string ftpThumbFullPath = null; // DB'ye yazılacak yol
                    string ftpImagesEquipFolder = null; // FTP'deki son klasör

                    // 2.1 İsimlendirme ve Klasör Yapısı
                    string safeJobName = SanitizeFolderName(job.JobName);
                    string safeEquipName = SanitizeFolderName(equip.Name);
                    string thumbFileName = $"{Path.GetFileNameWithoutExtension(sourceLocalPath)}_thumb.png";

                    // Klasör İsimleri
                    string jobFolder = $"{job.JobNumber}_{safeJobName}";
                    string equipFolder = $"{job.JobNumber}_{equip.EquipmentId}_{safeEquipName}";

                    // HEDEF FTP YOLLARI (İsteğine uygun yapı)
                    // Örnek: Attachments/Images/50_Deneme50/50_1_D1/
                    string ftpImagesBase = "Attachments/Images";
                    string ftpImagesJobFolder = $"{ftpImagesBase}/{jobFolder}";
                    ftpImagesEquipFolder = $"{ftpImagesJobFolder}/{equipFolder}";

                    // DB'ye kaydedilecek tam yol
                    ftpThumbFullPath = $"{ftpImagesEquipFolder}/{thumbFileName}";

                    bool thumbnailCreated = false;

                    try
                    {
                        MainThread.BeginInvokeOnMainThread(() => attachment.ProcessingProgress = 0.6);

                        // 2.2 Geçici Klasörde Resmi Oluştur
                        string tempDir = Path.Combine(FileSystem.CacheDirectory, Guid.NewGuid().ToString());
                        if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                        tempThumbPath = Path.Combine(tempDir, thumbFileName);

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
                                    DrawType = Aspose.CAD.FileFormats.Cad.CadDrawTypeMode.UseObjectColor,
                                    NoScaling = false
                                };
                                cadImage.Save(tempThumbPath, new PngOptions { VectorRasterizationOptions = rasterizationOptions });
                            }
                        });

                        if (File.Exists(tempThumbPath))
                        {
                            thumbnailCreated = true;
                        }
                    }
                    catch (Exception imgEx)
                    {
                        Debug.WriteLine($"Resim Oluşturma Hatası: {imgEx.Message}");
                    }

                    // -- AŞAMA 3: DB GÜNCELLEME VE RESİM YÜKLEME --
                    if (thumbnailCreated)
                    {
                        try
                        {
                            // 3.1 ÖNCE VERİTABANINI GÜNCELLE (Garanti olsun)
                            var dbRecord = await dbContext.EquipmentAttachments.FindAsync(attachment.Id);
                            if (dbRecord != null)
                            {
                                dbRecord.ThumbnailPath = ftpThumbFullPath;
                                await dbContext.SaveChangesAsync();
                            }

                            // 3.2 UI'ı Hemen Güncelle (Local path ile)
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                attachment.ThumbnailPath = tempThumbPath;
                                attachment.ProcessingProgress = 0.8;
                            });

                            // 3.3 FTP Klasörlerini Sırayla Oluştur
                            await _ftpHelper.CreateDirectoryAsync("Attachments");
                            await _ftpHelper.CreateDirectoryAsync(ftpImagesBase);       // Attachments/Images
                            await _ftpHelper.CreateDirectoryAsync(ftpImagesJobFolder);  // Attachments/Images/50_Deneme
                            await _ftpHelper.CreateDirectoryAsync(ftpImagesEquipFolder);// Attachments/Images/50_Deneme/50_1_D1

                            // 3.4 Dosyayı Yükle
                            await _ftpHelper.UploadFileAsync(tempThumbPath, ftpImagesEquipFolder);
                        }
                        catch (Exception uploadEx)
                        {
                            Debug.WriteLine($"Thumbnail Yükleme Hatası: {uploadEx.Message}");
                        }
                    }
                }

                // -- AŞAMA 4: TEMİZLİK VE BİTİŞ --
                // Sadece kullanıcı (Admin değilse) geçici dosyayı temizle
                bool isAdmin = App.CurrentUser?.IsAdmin ?? false;
                if (!isAdmin && File.Exists(sourceLocalPath))
                {
                    try { File.Delete(sourceLocalPath); } catch { }
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    attachment.ProcessingProgress = 1.0;
                    attachment.IsProcessing = false;

                    // UI Refresh tetiklemesi
                    if (!string.IsNullOrEmpty(attachment.ThumbnailPath))
                    {
                        string temp = attachment.ThumbnailPath;
                        attachment.ThumbnailPath = null;
                        attachment.ThumbnailPath = temp;
                    }
                });
            }
        }

        public async Task<EquipmentAttachment> AddAttachmentAsync(JobModel parentJob, Equipment parentEquipment, FileResult fileToCopy)
        {
            // 1. Yerel Yolları Hazırla
            string dbPath = GetBaseDatabasePath();
            string baseAttachmentPath = Path.Combine(dbPath, "Attachments");

            string safeJobName = SanitizeFolderName(parentJob.JobName);
            string safeEquipName = SanitizeFolderName(parentEquipment.Name);

            string jobFolder = $"{parentJob.JobNumber}_{safeJobName}";
            string equipFolder = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{safeEquipName}";

            string targetDirectory = Path.Combine(baseAttachmentPath, jobFolder, equipFolder);
            if (!Directory.Exists(targetDirectory)) Directory.CreateDirectory(targetDirectory);

            // 2. Dosyayı Kopyala
            string uniqueFilePath = GetUniqueFilePath(targetDirectory, fileToCopy.FileName);
            string uniqueFileName = Path.GetFileName(uniqueFilePath);

            using (var sourceStream = await fileToCopy.OpenReadAsync())
            using (var targetStream = File.Create(uniqueFilePath))
            {
                await sourceStream.CopyToAsync(targetStream);
            }

            // 3. DB Kaydı (Thumbnail başlangıçta NULL)
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

            // 4. Arka Plan İşlemini Başlat
            _ = Task.Run(() => ProcessAttachmentInBackground(newAttachment, uniqueFilePath, ftpRelativePath, parentJob, parentEquipment));

            return newAttachment;
        }

        // --- YENİ EKLENEN UPDATE METODU ---
        public async Task<EquipmentAttachment> UpdateAttachmentAsync(EquipmentAttachment existingAttachment, JobModel parentJob, Equipment parentEquipment, FileResult newFile)
        {
            // 1. Yerel Yollar
            string dbPath = GetBaseDatabasePath();
            string baseAttachmentPath = Path.Combine(dbPath, "Attachments");

            string safeJobName = SanitizeFolderName(parentJob.JobName);
            string safeEquipName = SanitizeFolderName(parentEquipment.Name);
            string jobFolder = $"{parentJob.JobNumber}_{safeJobName}";
            string equipFolder = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{safeEquipName}";

            string targetDirectory = Path.Combine(baseAttachmentPath, jobFolder, equipFolder);
            if (!Directory.Exists(targetDirectory)) Directory.CreateDirectory(targetDirectory);

            // 2. Eski Dosyayı Sil (Local)
            string oldLocalPath = "";
            if (!string.IsNullOrEmpty(existingAttachment.FilePath))
            {
                string relativePath = existingAttachment.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString());
                oldLocalPath = Path.Combine(dbPath, relativePath);
            }
            else
            {
                oldLocalPath = existingAttachment.FilePath;
            }

            if (File.Exists(oldLocalPath)) { try { File.Delete(oldLocalPath); } catch { } }

            // Eski Thumbnail'i de temizle (Local cache veya temp)
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

            // 4. DB Güncelle (Thumbnail'i sıfırla ki yenisi gelsin)
            string ftpRelativePath = $"Attachments/{jobFolder}/{equipFolder}/{uniqueFileName}";

            existingAttachment.FileName = uniqueFileName;
            existingAttachment.FilePath = ftpRelativePath;
            existingAttachment.ThumbnailPath = null;
            existingAttachment.IsProcessing = true;
            existingAttachment.ProcessingProgress = 0;

            _context.EquipmentAttachments.Update(existingAttachment);
            await _context.SaveChangesAsync();
            _context.Entry(existingAttachment).State = EntityState.Detached;

            // 5. Arka Plan İşlemini Başlat (Resmi yeniden oluşturacak)
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
            var entry = _context.EquipmentAttachments.Attach(attachment);
            entry.State = EntityState.Deleted;
            await _context.SaveChangesAsync();

            if (IsAdmin)
            {
                string dbPath = GetBaseDatabasePath();
                string localPath = Path.Combine(dbPath, attachment.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
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