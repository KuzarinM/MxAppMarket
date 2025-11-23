using ChocolateyAppMaker.Data;
using ChocolateyAppMaker.Models.DB;
using ChocolateyAppMaker.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChocolateyAppMaker.Repositories.Implementations
{
    public class InstallerRepository:IInstallerRepository
    {
        private readonly AppDbContext _context;

        public InstallerRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<InstallerFile>> GetInstallersByPathPrefixAsync(string pathPrefix)
        {
            // SQLite не любит сложные сравнения путей, используем Contains для надежности
            return await _context.InstallerFiles
                .Include(f => f.SoftwareProfile)
                .Where(f => f.FilePath.Contains(pathPrefix))
                .ToListAsync();
        }

        public async Task<int> GetCountByPathPrefixAsync(string pathPrefix)
        {
            return await _context.InstallerFiles
                .Where(f => f.FilePath.Contains(pathPrefix))
                .CountAsync();
        }
        public async Task<List<InstallerFile>> GetAllInstallersAsync()
        {
            return await _context.InstallerFiles
                .Include(f => f.SoftwareProfile)
                .ToListAsync();
        }
        public async Task<InstallerFile?> GetInstallerByIdAsync(int id)
        {
            return await _context.InstallerFiles
                .Include(f => f.SoftwareProfile)
                .FirstOrDefaultAsync(f => f.Id == id);
        }
        public async Task AddInstallerAsync(InstallerFile installer)
        {
            await _context.InstallerFiles.AddAsync(installer);
        }
        public Task RemoveInstallerAsync(InstallerFile installer)
        {
            _context.InstallerFiles.Remove(installer);

            return Task.CompletedTask;
        }
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
