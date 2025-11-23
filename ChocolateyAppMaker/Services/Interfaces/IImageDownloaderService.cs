namespace ChocolateyAppMaker.Services.Interfaces
{
    public interface IImageDownloaderService
    {
        Task<string> SaveImageAsync(string remoteUrl, string packageId, string prefix);
        Task<string> SaveUploadAsync(IFormFile file, string packageId, string prefix);
        void DeleteFile(string webPath);
    }
}
