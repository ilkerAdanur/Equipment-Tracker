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
                // Örn: ftp://ip_adresi/Attachments/32_Askale
                string targetUrl = CombineFtpPath(folderPath);

                WebRequest request = WebRequest.Create(targetUrl);
                request.Method = WebRequestMethods.Ftp.MakeDirectory;
                request.Credentials = new NetworkCredential(User, Pass);

                using (var resp = (FtpWebResponse)await request.GetResponseAsync())
                {
                    // 250 veya 257 kodu başarılı demektir.
                    // Klasör zaten varsa hata verebilir, catch bloğunda yutacağız.
                }
            }
            catch (WebException ex)
            {
                // Klasör zaten varsa "550" hatası döner, bu bir sorun değil.
                FtpWebResponse response = (FtpWebResponse)ex.Response;
                if (response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    // Klasör zaten var, devam et.
                    return;
                }
                // Diğer hatalar için loglama yapabilirsin
                System.Diagnostics.Debug.WriteLine($"FTP Klasör Hatası: {ex.Message}");
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
                    await client.UploadFileTaskAsync(new Uri(targetUrl), WebRequestMethods.Ftp.UploadFile, localFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FTP Yükleme Hatası ({localFilePath}): {ex.Message}");
                // İstersen burada kullanıcıya hata mesajı döndürebilirsin
            }
        }

        // FTP adresini düzgün birleştirmek için yardımcı
        private string CombineFtpPath(string pathSuffix)
        {
            // Host: ftp://46.202...
            string baseHost = Host.TrimEnd('/');

            // PathSuffix: Attachments\32_Askale (Windows ters slash yapabilir)
            // FTP forward slash (/) sever.
            string cleanSuffix = pathSuffix.Replace("\\", "/").TrimStart('/');

            return $"{baseHost}/{cleanSuffix}";
        }
    }
}