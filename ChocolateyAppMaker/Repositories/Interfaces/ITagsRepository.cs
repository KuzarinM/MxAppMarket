namespace ChocolateyAppMaker.Repositories.Interfaces
{
    public interface ITagsRepository
    {
        Task<List<string>> GetAllTagsAsync();
    }
}
