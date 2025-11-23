using ChocolateyAppMaker.Data;
using ChocolateyAppMaker.Models.DB;
using ChocolateyAppMaker.Models.Dtos;
using ChocolateyAppMaker.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChocolateyAppMaker.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly IProfileRepository _profileRepository;

        public IndexModel(IProfileRepository profileRepository)
        {
            _profileRepository = profileRepository;
        }

        public PagedResult<SoftwareProfile> Profiles { get; set; } = default!;

        [BindProperty(SupportsGet = true)]
        public int P { get; set; } = 1;

        // 1. Добавляем свойство для поиска
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!User.IsInRole("Admin")) return RedirectToPage("/Store/Index");

            if (P < 1) P = 1;
            int pageSize = 20;

            // 2. Передаем SearchTerm в метод репозитория
            Profiles = await _profileRepository.GetAdminProfilesAsync(P, pageSize, SearchTerm);

            return Page();
        }
    }
}
