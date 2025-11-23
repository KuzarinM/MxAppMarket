namespace ChocolateyAppMaker.Services.Interfaces
{
    public interface IScannerService
    {
        Task<int> ScanFolderAsync(string rootPath);
        Task<int> RunDeduplicationAsync();
    }
}
