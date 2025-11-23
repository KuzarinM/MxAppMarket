using ChocolateyAppMaker.Models.DB;

namespace ChocolateyAppMaker.Services.Interfaces
{
    public interface IChocolateyBuilderService
    {
        Task<string> BuildPackageAsync(SoftwareProfile profile, InstallerFile file);
    }
}
