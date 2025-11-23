using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChocolateyAppMaker.Pages.Account
{
    public class AccessDeniedModel : PageModel
    {
        public IActionResult OnGet()
        {
            // Если пользователь залогинен, но полез куда не надо -> в Магазин
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Store/Index");
            }

            // Если аноним (вдруг) -> на логин
            return RedirectToPage("/Account/Login");
        }
    }
}
