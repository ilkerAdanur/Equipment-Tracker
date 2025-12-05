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

        private async Task ProcessAttachmentInBackground(EquipmentAttachment attachment, string sourceLocalPath, string ftpRelativePath, JobModel job, Equipment equip)
        {
            // Null Kontrolü
            if (job == null || equip == null) return;

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

                    string jobFolder = $"{job.JobNumber}_{safeJobName}";
                    string equipFolder = $"{job.JobNumber}_{equip.EquipmentId}_{safeEquipName}";

                    // 1. FTP ANA DOSYA YÜKLEME
                    string ftpFolderPath = Path.GetDirectoryName(ftpRelativePath).Replace("\\", "/");

                    var uploadProgress = new Progress<double>(percent =>
                    {
                        MainThread.BeginInvokeOnMainThread(() => attachment.ProcessingProgress = percent * 0.6);
                    });

                    // Klasörleri Sırayla Oluştur (Attachments -> Is -> Ekipman)
                    await _ftpHelper.CreateDirectoryAsync("Attachments");
                    await _ftpHelper.CreateDirectoryAsync($"Attachments/{jobFolder}");
                    await _ftpHelper.CreateDirectoryAsync($"Attachments/{jobFolder}/{equipFolder}");

                    // Dosyayı Yükle
                    await _ftpHelper.UploadFileWithProgressAsync(sourceLocalPath, ftpFolderPath, uploadProgress);

                    // 2. THUMBNAIL İŞLEMLERİ (DWG, DXF ve RESİMLER)
                    string extension = Path.GetExtension(sourceLocalPath).ToLower();
                    var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
                    bool isDwg = extension == ".dwg" || extension == ".dxf";
                    bool isImage = imageExtensions.Contains(extension);

                    if (isDwg || isImage)
                    {
                        MainThread.BeginInvokeOnMainThread(() => attachment.ProcessingProgress = 0.65);

                        string realThumbName = $"{Path.GetFileNameWithoutExtension(sourceLocalPath)}_thumb.png";

                        // FTP Resim Yolu: Attachments/Images/Job/Equip
                        string ftpImagesBase = "Attachments/Images";
                        string ftpImagesPath = $"{ftpImagesBase}/{jobFolder}/{equipFolder}";
                        string ftpThumbFullPath = $"{ftpImagesPath}/{realThumbName}";

                        // Geçici Klasör (Benzersiz GUID ile kilitlenmeyi önler)
                        string tempDir = Path.Combine(FileSystem.CacheDirectory, Guid.NewGuid().ToString());
                        if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
                        string safeTempPath = Path.Combine(tempDir, realThumbName);

                        bool thumbnailCreated = false;

                        await Task.Run(() =>
                        {
                            try
                            {
                                if (isDwg)
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
                                        // DXF için Model kontrolü
                                        if (cadImage is Aspose.CAD.FileFormats.Cad.CadImage cad && cad.Layouts.ContainsKey("Model"))
                                        {
                                            rasterizationOptions.Layouts = new[] { "Model" };
                                        }
                                        else
                                        {
                                            rasterizationOptions.Layouts = null; // Varsayılan
                                            rasterizationOptions.AutomaticLayoutsScaling = true;
                                        }

                                        cadImage.Save(safeTempPath, new PngOptions { VectorRasterizationOptions = rasterizationOptions });
                                    }
                                }
                                else if (isImage)
                                {
                                    // Resimse direkt kopyala
                                    File.Copy(sourceLocalPath, safeTempPath, true);
                                }

                                thumbnailCreated = File.Exists(safeTempPath);
                            }
                            catch (Exception imgEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"Resim Oluşturma Hatası: {imgEx.Message}");
                            }
                        });

                        if (thumbnailCreated)
                        {
                            // Veritabanını Güncelle
                            var dbRecord = await dbContext.EquipmentAttachments.FindAsync(attachment.Id);
                            if (dbRecord != null)
                            {
                                dbRecord.ThumbnailPath = ftpThumbFullPath;
                                await dbContext.SaveChangesAsync();
                            }

                            // UI Güncelle (Geçici yolla hemen göster)
                            MainThread.BeginInvokeOnMainThread(() => attachment.ThumbnailPath = safeTempPath);

                            // FTP'ye Yükle
                            MainThread.BeginInvokeOnMainThread(() => attachment.ProcessingProgress = 0.8);

                            await _ftpHelper.CreateDirectoryAsync("Attachments");
                            await _ftpHelper.CreateDirectoryAsync(ftpImagesBase);
                            await _ftpHelper.CreateDirectoryAsync($"{ftpImagesBase}/{jobFolder}");
                            await _ftpHelper.CreateDirectoryAsync(ftpImagesPath);

                            await _ftpHelper.UploadFileAsync(safeTempPath, ftpImagesPath);
                        }
                    }

                    // Temizlik
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
                    System.Diagnostics.Debug.WriteLine($"Ekipman Arka Plan Hatası: {ex.Message}");
                }
                finally
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        attachment.IsProcessing = false;
                        // UI Tetiklemesi
                        if (!string.IsNullOrEmpty(attachment.ThumbnailPath))
                        {
                            var p = attachment.ThumbnailPath;
                            attachment.ThumbnailPath = null;
                            attachment.ThumbnailPath = p;
                        }
                    });
                }
            }
        }


        public async Task<EquipmentAttachment> AddAttachmentAsync(JobModel parentJob, Equipment parentEquipment, FileResult fileToCopy)
        {
            // 1. Klasör İsimlerini Hazırla
            string safeJobName = SanitizeFolderName(parentJob.JobName);
            string safeEquipName = SanitizeFolderName(parentEquipment.Name);

            string jobFolder = $"{parentJob.JobNumber}_{safeJobName}";
            string equipFolder = $"{parentJob.JobNumber}_{parentEquipment.EquipmentId}_{safeEquipName}";

            // 2. HEDEF KONUMLAR (Admin vs User Ayrımı)
            // Bu kısım paylaştığınız kodda eksikti.
            string localTargetDir;

            if (IsAdmin)
            {
                // Admin: Kalıcı Klasör (Belgelerim/TrackerDatabase/...)
                string dbPath = GetBaseDatabasePath();
                string baseAttachmentPath = Path.Combine(dbPath, "Attachments");
                localTargetDir = Path.Combine(baseAttachmentPath, jobFolder, equipFolder);
            }
            else
            {
                // Normal User: Geçici Klasör (Cache/TempUploads)
                // Dosyalar burada kalıcı olmaz, FTP'ye gidince silinir.
                localTargetDir = Path.Combine(FileSystem.CacheDirectory, "TempUploads");
            }

            // Klasör yoksa oluştur
            if (!Directory.Exists(localTargetDir)) Directory.CreateDirectory(localTargetDir);

            // 3. Dosyayı Kopyala (İsim çakışmasını önleyerek)
            string uniqueFilePath = GetUniqueFilePath(localTargetDir, fileToCopy.FileName);
            string uniqueFileName = Path.GetFileName(uniqueFilePath);

            using (var sourceStream = await fileToCopy.OpenReadAsync())
            using (var targetStream = File.Create(uniqueFilePath))
            {
                await sourceStream.CopyToAsync(targetStream);
            }

            string ftpRelativePath = $"Attachments/{jobFolder}/{equipFolder}/{uniqueFileName}";

            var newAttachment = new EquipmentAttachment
            {
                FileName = uniqueFileName,
                FilePath = ftpRelativePath, // Relative Path (Göreli Yol)
                ThumbnailPath = null,
                EquipmentId = parentEquipment.Id,
                IsProcessing = true, // Yükleme başlıyor
                ProcessingProgress = 0
            };

            _context.EquipmentAttachments.Add(newAttachment);
            await _context.SaveChangesAsync();
            _context.Entry(newAttachment).State = EntityState.Detached;

            // 5. Arka Plan İşlemini Başlat
            // ftpRelativePath parametresini gönderdiğimizden emin oluyoruz.
            _ = Task.Run(() => ProcessAttachmentInBackground(newAttachment, uniqueFilePath, ftpRelativePath, parentJob, parentEquipment));

            return newAttachment;
        }

        // --- YENİ EKLENEN UPDATE METODU ---
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

            // 2. Eski Dosyayı Silme Kontrolü
            // Eğer dosya ismi değiştiyse eski dosyayı silmeye çalışırız.
            // Ancak dosya açıksa hata verebilir, bunu "try-catch" ile yutalım ki işlem durmasın.
            string oldLocalPath = "";
            if (!string.IsNullOrEmpty(existingAttachment.FilePath))
            {
                string relativePath = existingAttachment.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString());
                oldLocalPath = Path.Combine(dbPath, relativePath);
            }

            if (existingAttachment.FileName != newFile.FileName && File.Exists(oldLocalPath))
            {
                try { File.Delete(oldLocalPath); } catch { /* Dosya silinemezse de devam et */ }
            }

            // Thumbnail silme (Önemsiz, hata verirse geç)
            if (!string.IsNullOrEmpty(existingAttachment.ThumbnailPath) && File.Exists(existingAttachment.ThumbnailPath))
            {
                try { File.Delete(existingAttachment.ThumbnailPath); } catch { }
            }

            // 3. Yeni Dosyayı Kopyala (KİLİTLENME KONTROLÜ)
            string finalFilePath;
            string finalFileName;

            if (existingAttachment.FileName == newFile.FileName)
            {
                // İsim aynı: Üzerine yazılacak
                finalFilePath = Path.Combine(targetDirectory, newFile.FileName);
                finalFileName = newFile.FileName;

                // --- KRİTİK DÜZELTME ---
                // Dosya varsa silmeyi dene. Eğer dosya açıksa hata fırlat ve kullanıcıyı uyar.
                if (File.Exists(finalFilePath))
                {
                    try
                    {
                        File.Delete(finalFilePath);
                    }
                    catch (IOException)
                    {
                        // Bu hata ViewModel'e gidecek ve ekranda Alert olarak görünecek
                        throw new Exception($"'{finalFileName}' dosyası şu anda açık! Lütfen dosyayı kapatıp tekrar deneyin.");
                    }
                    catch (UnauthorizedAccessException)
                    {
                        throw new Exception($"'{finalFileName}' dosyasını değiştirmek için yetkiniz yok.");
                    }
                }
            }
            else
            {
                // İsim farklı: Benzersiz isim oluştur
                finalFilePath = GetUniqueFilePath(targetDirectory, newFile.FileName);
                finalFileName = Path.GetFileName(finalFilePath);
            }

            using (var sourceStream = await newFile.OpenReadAsync())
            using (var targetStream = File.Create(finalFilePath))
            {
                await sourceStream.CopyToAsync(targetStream);
            }

            // 4. Veritabanı Güncelle
            string ftpRelativePath = $"Attachments/{jobFolder}/{equipFolder}/{finalFileName}";

            existingAttachment.FileName = finalFileName;
            existingAttachment.FilePath = ftpRelativePath;
            existingAttachment.ThumbnailPath = null;
            existingAttachment.IsProcessing = true;
            existingAttachment.ProcessingProgress = 0;

            try
            {
                _context.EquipmentAttachments.Update(existingAttachment);
                await _context.SaveChangesAsync();
                _context.Entry(existingAttachment).State = EntityState.Detached;

                // 5. Arka Plan İşlemi
                _ = Task.Run(() => ProcessAttachmentInBackground(existingAttachment, finalFilePath, ftpRelativePath, parentJob, parentEquipment));
            }
            catch (Exception)
            {
                existingAttachment.IsProcessing = false;
                throw;
            }

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