using ChocolateyAppMaker.Models.DB;
using ChocolateyAppMaker.Models.Dtos;
using ChocolateyAppMaker.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChocolateyAppMaker.Pages.Store
{
    public class IndexModel : PageModel
    {
        private readonly ITagsRepository _tagsRepository;
        private readonly IProfileRepository _profileRepository;

        public IndexModel(ITagsRepository tagsRepository, IProfileRepository profileRepository)
        {
            _tagsRepository = tagsRepository;
            _profileRepository = profileRepository;
        }

        public PagedResult<SoftwareProfile> Data { get; set; }
        public List<string> AllTags { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string SearchTerm { get; set; }

        // Здесь храним список выбранных тегов через запятую (например: "python,dev,tools")
        [BindProperty(SupportsGet = true)]
        public string Tag { get; set; }

        [BindProperty(SupportsGet = true)]
        public int P { get; set; } = 1;

        // Вспомогательное свойство: парсит строку Tag в список для удобства в View
        public List<string> ActiveTagsList => string.IsNullOrWhiteSpace(Tag)
            ? new List<string>()
            : Tag.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        public async Task OnGetAsync()
        {
            if (P < 1) P = 1;
            int pageSize = 12;

            // Загружаем данные
            Data = await _profileRepository.GetStoreProfilesAsync(SearchTerm, Tag, P, pageSize);

            // Загружаем все теги для облака
            AllTags = await _tagsRepository.GetAllTagsAsync();
        }

        // --- ХЕЛПЕРЫ ДЛЯ VIEW (ЧИСТАЯ ЛОГИКА) ---

        /// <summary>
        /// Генерирует новую строку тегов: если тег есть - удаляет, если нет - добавляет.
        /// </summary>
        public string GetToggleTagParam(string tagToToggle)
        {
            var currentTags = ActiveTagsList; // Получаем текущий список (List<string>)
            var normalizedTag = tagToToggle.Trim();

            // Проверяем наличие (ignoring case)
            var existing = currentTags.FirstOrDefault(t => t.Equals(normalizedTag, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                // Если есть - удаляем
                currentTags.Remove(existing);
            }
            else
            {
                // Если нет - добавляем
                currentTags.Add(normalizedTag);
            }

            // Собираем обратно в строку через запятую
            return string.Join(",", currentTags);
        }

        public bool IsTagActive(string tag)
        {
            return ActiveTagsList.Contains(tag, StringComparer.OrdinalIgnoreCase);
        }
    }
}
