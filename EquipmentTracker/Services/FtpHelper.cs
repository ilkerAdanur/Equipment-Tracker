using System.Net;
using System.Text;

namespace EquipmentTracker.Services
{
    public class FtpHelper
    {
        // Ayarları Preferences'tan okuyan yardımcı özellikler
        private string Host => Preferences.Get("FtpHost", "");
        private string User => Preferences.Get("FtpUser", "");
        private string Pass => Preferences.Get("FtpPassword", "");

        // FTP Ana dizini (public_html/TrackerDatabase)
        // Hostinger'da genellikle kullanıcı kök dizine atar, o yüzden path'i dinamik birleştireceğiz.

        public async Task CreateDirectoryAsync(string folderPath)
        {
            if (string.IsNullOrEmpty(Host)) return;

            try
            {
                string targetUrl = CombineFtpPath(folderPath);
                WebRequest request = WebRequest.Create(targetUrl);
                request.Method = WebRequestMethods.Ftp.MakeDirectory;
                request.Credentials = new NetworkCredential(User, Pass);

                using (var resp = (FtpWebResponse)await request.GetResponseAsync()) { }
            }
            catch (WebException ex)
            {
                FtpWebResponse response = (FtpWebResponse)ex.Response;
                if (response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable) return; // Klasör zaten var
            }
        }

        public async Task DownloadFileAsync(string remotePath, string localPath)
        {
            if (string.IsNullOrEmpty(Host)) return;

            try
            {
                string localDir = Path.GetDirectoryName(localPath);
                if (!Directory.Exists(localDir)) Directory.CreateDirectory(localDir);

                string targetUrl = CombineFtpPath(remotePath);

                using (var client = new WebClient())
                {
                    client.Credentials = new NetworkCredential(User, Pass);
                    await client.DownloadFileTaskAsync(new Uri(targetUrl), localPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FTP İndirme Hatası: {ex.Message}");
            }
        }

        public async Task UploadFileAsync(string localFilePath, string remoteFolderPath)
        {
            if (string.IsNullOrEmpty(Host)) return;

            try
            {
                string fileName = Path.GetFileName(localFilePath);
                // Örn: ftp://ip/Attachments/32_Askale/dosya.pdf
                string targetUrl = CombineFtpPath(remoteFolderPath) + "/" + fileName;

                using (var client = new WebClient())
                {
                    client.Credentials = new NetworkCredential(User, Pass);
                    // UploadFileTaskAsync kullanıyoruz
                    await UploadFileWithProgressAsync(localFilePath, remoteFolderPath, null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FTP Yükleme Hatası ({localFilePath}): {ex.Message}");
                // İstersen burada kullanıcıya hata mesajı döndürebilirsin
            }
        }

        public async Task UploadFileWithProgressAsync(string localFilePath, string remoteFolderPath, IProgress<double> progress)
        {
            if (string.IsNullOrEmpty(Host)) return;

            try
            {
                string fileName = Path.GetFileName(localFilePath);
                string targetUrl = CombineFtpPath(remoteFolderPath) + "/" + fileName;

                using (var client = new WebClient())
                {
                    client.Credentials = new NetworkCredential(User, Pass);

                    // İlerleme olayını dinle
                    client.UploadProgressChanged += (s, e) =>
                    {
                        // Yüzdeyi 0.0 - 1.0 arasına çevirip bildir
                        progress?.Report(e.ProgressPercentage / 100.0);
                    };

                    await client.UploadFileTaskAsync(new Uri(targetUrl), WebRequestMethods.Ftp.UploadFile, localFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FTP Yükleme Hatası ({localFilePath}): {ex.Message}");
                throw; // Hatayı yukarı fırlat ki Service haberdar olsun
            }
        }

        private string CombineFtpPath(string pathSuffix)
        {
            string baseHost = Host.TrimEnd('/');
            string cleanSuffix = pathSuffix.Replace("\\", "/").TrimStart('/');
            return $"{baseHost}/{cleanSuffix}";
        }

        public async Task RenameFileOrDirectoryAsync(string oldPathSuffix, string newPathSuffix)
        {
            if (string.IsNullOrEmpty(Host)) return;

            try
            {
                // Eski yol (ftp://.../Attachments/EskiIsim)
                string targetUrl = CombineFtpPath(oldPathSuffix);

                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(targetUrl);
                request.Method = WebRequestMethods.Ftp.Rename;
                request.Credentials = new NetworkCredential(User, Pass);

                // PÜF NOKTASI: Bazı sunucular RenameTo için kök dizinden başlayan tam yol ister.
                // newPathSuffix şuna benzemeli: "/public_html/Attachments/YeniIsim" veya sadece "Attachments/YeniIsim"
                // Bizim CombineFtpPath metodu "ftp://" ekliyor, onu kullanmayacağız.
                // Direkt parametre olarak gelen temiz yolu veriyoruz.
                request.RenameTo = newPathSuffix;

                using (var response = (FtpWebResponse)await request.GetResponseAsync())
                {
                    // Başarılı
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FTP Rename Hatası: {ex.Message}");
                // Hata olsa bile devam etsin, belki yeni klasör oluşturulup dosyalar oraya atılır
            }
        }



    }
}