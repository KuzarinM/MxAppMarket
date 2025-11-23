using ChocolateyAppMaker.Data;
using ChocolateyAppMaker.Models.DB;
using ChocolateyAppMaker.Models.Dtos;
using ChocolateyAppMaker.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChocolateyAppMaker.Repositories.Implementations
{
    public class ProfileRepository: IProfileRepository
    {
        private readonly AppDbContext _context;

        public ProfileRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<SoftwareProfile>> GetAllProfilesAsync()
        {
            return await _context.SoftwareProfiles
                .Include(p => p.Installers)
                .OrderByDescending(p => p.UpdatedAt)
                .ToListAsync();
        }

        public async Task<PagedResult<SoftwareProfile>> GetAdminProfilesAsync(int pageIndex, int pageSize, string? searchTerm = null)
        {
            var query = _context.SoftwareProfiles
                .Include(p => p.Installers)
                .AsQueryable();

            // --- ЛОГИКА ПОИСКА ---
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(term) ||
                                         (p.Tags != null && p.Tags.ToLower().Contains(term)) ||
                                         (p.ChocolateyId != null && p.ChocolateyId.ToLower().Contains(term)));
            }
            // ---------------------

            query = query.OrderByDescending(p => p.UpdatedAt);

            var count = await query.CountAsync();
            var items = await query.Skip((pageIndex - 1) * pageSize)
                                   .Take(pageSize)
                                   .ToListAsync();

            return new PagedResult<SoftwareProfile>(items, count, pageIndex, pageSize);
        }

        public async Task<SoftwareProfile?> GetProfileAsync(int? id = null, string? name = null, string? folderName = null)
        {
            IQueryable<SoftwareProfile> query = _context.SoftwareProfiles.Include(p => p.Installers);

            if (id != null)
            {
                query = query.Where(x => x.Id == id);
            }
            else if (name != null)
            {
                query = query.Where(x => x.Name.ToLower() == name.ToLower());
            }
            else if (folderName != null) 
            {
                query = query.Where(x => 
                                    x.FolderName!= null 
                                    && x.FolderName.ToLower() == folderName.ToLower()
                                    );
            }

            return await query.FirstOrDefaultAsync();
        }

        public async Task AddProfileAsync(SoftwareProfile profile)
        {
            await _context.SoftwareProfiles.AddAsync(profile);
        }

        public Task UpdateProfileAsync(SoftwareProfile profile)
        {
            _context.SoftwareProfiles.Update(profile);
            return Task.CompletedTask;
        }

        public Task RemoveProfileAsync(SoftwareProfile profile)
        {
            _context.SoftwareProfiles.Remove(profile);

            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<PagedResult<SoftwareProfile>> GetStoreProfilesAsync(string? searchTerm, string? tag, int pageIndex, int pageSize)
        {
            var query = _context.SoftwareProfiles
                        .Include(p => p.Installers)
                        .AsQueryable();

            // 1. Поиск по тексту (без изменений)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(term) ||
                                         p.Description != null && p.Description.ToLower().Contains(term));
            }

            // 2. МУЛЬТИ-ТЕГ ФИЛЬТР (Логика "AND")
            if (!string.IsNullOrWhiteSpace(tag))
            {
                // Разбиваем входную строку "python, dev" на ["python", "dev"]
                var tags = tag.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var tRaw in tags)
                {
                    var t = tRaw.ToLower().Trim();

                    // Добавляем условие Where для КАЖДОГО тега. 
                    // В EF Core цепочка .Where().Where() работает как SQL AND.
                    query = query.Where(p => p.Tags != null && (
                        p.Tags.ToLower() == t ||                 // Точное совпадение
                        p.Tags.ToLower().StartsWith(t + " ") ||  // Начало строки
                        p.Tags.ToLower().EndsWith(" " + t) ||    // Конец строки
                        p.Tags.ToLower().Contains(" " + t + " ") // В середине
                    ));
                }
            }

            // 3. Сортировка
            query = query.OrderByDescending(p => p.UpdatedAt);

            // 4. Пагинация
            var count = await query.CountAsync();
            var items = await query.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();

            return new PagedResult<SoftwareProfile>(items, count, pageIndex, pageSize);
        }

        public async Task<int> MergeDuplicatesAsync()
        {
            // 1. Загружаем все профили, у которых есть ChocolateyId
            var allProfiles = await _context.SoftwareProfiles
                .Include(p => p.Installers)
                .Where(p => p.ChocolateyId != null && p.ChocolateyId != "")
                .ToListAsync();

            // 2. Группируем их по ID (без учета регистра)
            var groups = allProfiles
                .GroupBy(p => p.ChocolateyId!.ToLower())
                .Where(g => g.Count() > 1)
                .ToList();

            int mergedProfilesCount = 0;

            foreach (var group in groups)
            {
                // Выбираем "Мастера" (например, тот, у которого больше всего файлов, или первый попавшийся)
                var master = group.OrderByDescending(p => p.Installers.Count)
                                  .ThenByDescending(p => p.UpdatedAt)
                                  .First();

                var duplicates = group.Where(p => p.Id != master.Id).ToList();

                foreach (var dup in duplicates)
                {
                    // Переносим файлы к мастеру
                    foreach (var installer in dup.Installers.ToList())
                    {
                        installer.SoftwareProfile = master;
                        // EF Core сам обновит SoftwareProfileId при сохранении
                    }

                    // Если у мастера нет описания/картинки, а у дубля есть - заберем
                    if (string.IsNullOrEmpty(master.Description) && !string.IsNullOrEmpty(dup.Description))
                        master.Description = dup.Description;

                    if (string.IsNullOrEmpty(master.IconUrl) && !string.IsNullOrEmpty(dup.IconUrl))
                        master.IconUrl = dup.IconUrl;

                    // Объединяем скриншоты (если нужно)
                    if (dup.Screenshots != null && dup.Screenshots.Any())
                    {
                        if (master.Screenshots == null) master.Screenshots = new List<string>();
                        foreach (var screen in dup.Screenshots)
                        {
                            if (!master.Screenshots.Contains(screen))
                                master.Screenshots.Add(screen);
                        }
                    }

                    // Удаляем дубликат профиля
                    _context.SoftwareProfiles.Remove(dup);
                    mergedProfilesCount++;
                }
            }

            await _context.SaveChangesAsync();
            return mergedProfilesCount;
        }
    }
}
