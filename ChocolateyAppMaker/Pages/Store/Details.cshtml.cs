using ChocolateyAppMaker.Models.DB;
using ChocolateyAppMaker.Repositories.Interfaces;
using ChocolateyAppMaker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Markdig;

namespace ChocolateyAppMaker.Pages.Store
{
    public class DetailsModel : PageModel
    {
        private readonly IInstallerRepository _installerRepository;
        private readonly IProfileRepository _profileRepository;

        public DetailsModel(IInstallerRepository installerRepository, IProfileRepository profileRepository)
        {
            _installerRepository = installerRepository;
            _profileRepository = profileRepository;
        }

        public SoftwareProfile Profile { get; set; } = default!;

        // 2. Свойство для хранения готового HTML
        public string DescriptionHtml { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var p = await _profileRepository.GetProfileAsync(id: id);
            if (p == null) return NotFound();
            Profile = p;

            // 3. Конвертация Markdown -> HTML
            if (!string.IsNullOrEmpty(Profile.Description))
            {
                // Настраиваем пайплайн (включаем таблицы, списки, авто-ссылки и т.д.)
                var pipeline = new MarkdownPipelineBuilder()
                    .UseAdvancedExtensions()
                    .Build();

                DescriptionHtml = Markdown.ToHtml(Profile.Description, pipeline);
            }
            else
            {
                DescriptionHtml = "<p class='text-muted'>Описание отсутствует.</p>";
            }

            return Page();
        }

        public async Task<IActionResult> OnGetDownloadAsync(int fileId)
        {
            var file = await _installerRepository.GetInstallerByIdAsync(fileId);
            if (file == null) return NotFound("Запись о файле не найдена");
            if (!System.IO.File.Exists(file.FilePath))
                return NotFound($"Файл физически отсутствует на сервере: {file.FilePath}");

            return PhysicalFile(file.FilePath, "application/octet-stream", file.Filename);
        }
    }
}
