using ChocolateyAppMaker.Data;
using ChocolateyAppMaker.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChocolateyAppMaker.Repositories.Implementations
{
    public class TagsRepository: ITagsRepository
    {
        private readonly AppDbContext _context;

        public TagsRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<string>> GetAllTagsAsync()
        {
            // Вытаскиваем все теги, разбиваем их в памяти и берем уникальные
            // Это не супер-оптимально для миллиона записей, но для каталога софта отлично
            var allTagStrings = await _context.SoftwareProfiles
                .Where(p => p.Tags != null && p.Tags != "")
                .Select(p => p.Tags)
                .ToListAsync();

            return allTagStrings
                .SelectMany(s => s.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(t => t.ToLower().Trim())
                .Distinct()
                .OrderBy(t => t)
                .ToList();
        }
    }
}
