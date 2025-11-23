using ChocolateyAppMaker.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace ChocolateyAppMaker.Services.Implementations
{
    public class ImageDownloaderService : IImageDownloaderService
    {
        private readonly HttpClient _httpClient;
        private readonly string _storagePath;

        public ImageDownloaderService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            // Берем путь, который настроили в Program.cs
            _storagePath = config["AppConfig:ImagesPhysicalPath"]
                           ?? throw new Exception("ImagesPhysicalPath not configured");
        }

        public async Task<string> SaveImageAsync(string remoteUrl, string packageId, string prefix)
        {
            if (string.IsNullOrEmpty(remoteUrl) || !remoteUrl.StartsWith("http")) return remoteUrl;

            // Папка: /app/data/images/{packageId}/
            var packageFolder = Path.Combine(_storagePath, packageId);
            if (!Directory.Exists(packageFolder)) Directory.CreateDirectory(packageFolder);

            try
            {
                var ext = Path.GetExtension(remoteUrl).Split('?')[0];
                if (string.IsNullOrEmpty(ext) || ext.Length > 5) ext = ".png";

                var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(remoteUrl)));
                var fileName = $"{prefix}_{hash}{ext}";
                var filePath = Path.Combine(packageFolder, fileName);

                // URL для браузера: /images/packages/{packageId}/{fileName}
                // Этот префикс должен совпадать с RequestPath в Program.cs
                var webUrl = $"/images/packages/{packageId}/{fileName}";

                if (File.Exists(filePath)) return webUrl;

                var imageBytes = await _httpClient.GetByteArrayAsync(remoteUrl);
                await File.WriteAllBytesAsync(filePath, imageBytes);

                return webUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Image DL Error: {ex.Message}");
                return remoteUrl;
            }
        }

        public async Task<string> SaveUploadAsync(IFormFile file, string packageId, string prefix)
        {
            if (file == null || file.Length == 0) return null;

            var packageFolder = Path.Combine(_storagePath, packageId);
            if (!Directory.Exists(packageFolder)) Directory.CreateDirectory(packageFolder);

            var ext = Path.GetExtension(file.FileName).ToLower();
            if (string.IsNullOrEmpty(ext)) ext = ".png";

            var fileName = $"{prefix}_{DateTime.Now.Ticks}{ext}";
            var filePath = Path.Combine(packageFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/images/packages/{packageId}/{fileName}";
        }

        public void DeleteFile(string webPath)
        {
            // webPath: "/images/packages/7zip/icon.png"
            if (string.IsNullOrEmpty(webPath)) return;

            // Убираем префикс "/images/packages/" чтобы получить "7zip/icon.png"
            var relative = webPath.Replace("/images/packages/", "").TrimStart('/');
            var fullPath = Path.Combine(_storagePath, relative);

            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
    }
}
