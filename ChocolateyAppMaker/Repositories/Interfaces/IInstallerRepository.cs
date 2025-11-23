using ChocolateyAppMaker.Models.DB;

namespace ChocolateyAppMaker.Repositories.Interfaces
{
    public interface IInstallerRepository
    {
        Task<List<InstallerFile>> GetAllInstallersAsync();
        Task<InstallerFile?> GetInstallerByIdAsync(int id);
        Task AddInstallerAsync(InstallerFile installer);
        Task RemoveInstallerAsync(InstallerFile installer);
        Task SaveChangesAsync();

        Task<List<InstallerFile>> GetInstallersByPathPrefixAsync(string pathPrefix);
        Task<int> GetCountByPathPrefixAsync(string pathPrefix);
    }
}
