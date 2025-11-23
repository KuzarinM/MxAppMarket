using ChocolateyAppMaker.Models.Dtos;
using ChocolateyAppMaker.Models.Enums;

namespace ChocolateyAppMaker.Services.Interfaces
{
    public interface IChocoMetadataService
    {
        Task<ChocoMetadataResult?> SearchPackageAsync(string softwareName, MetadataSourceType sourceType = MetadataSourceType.Auto);
    }
}
