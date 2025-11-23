using ChocolateyAppMaker.Models.DB;
using ChocolateyAppMaker.Models.Dtos;

namespace ChocolateyAppMaker.Repositories.Interfaces
{
    public interface IProfileRepository
    {
        Task<List<SoftwareProfile>> GetAllProfilesAsync();
        Task<PagedResult<SoftwareProfile>> GetAdminProfilesAsync(int pageIndex, int pageSize, string? searchTerm = null);
        Task<SoftwareProfile?> GetProfileAsync(int? id = null, string? name = null, string? folderName = null);
        Task AddProfileAsync(SoftwareProfile profile);
        Task UpdateProfileAsync(SoftwareProfile profile);
        Task RemoveProfileAsync(SoftwareProfile profile);
        Task SaveChangesAsync();


        Task<PagedResult<SoftwareProfile>> GetStoreProfilesAsync(
        string? searchTerm,
        string? tag,
        int pageIndex,
        int pageSize);

        Task<int> MergeDuplicatesAsync();
    }
}
