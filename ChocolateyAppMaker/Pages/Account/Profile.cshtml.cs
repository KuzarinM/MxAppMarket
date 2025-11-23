using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace ChocolateyAppMaker.Pages.Account
{
    public class ProfileModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public ProfileModel(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Display(Name = "Текущий пароль")]
            [DataType(DataType.Password)]
            public string? OldPassword { get; set; }

            [Display(Name = "Новый пароль")]
            [DataType(DataType.Password)]
            public string? NewPassword { get; set; }

            [Display(Name = "Новый логин")]
            public string? NewUserName { get; set; }
        }

        public string CurrentUserName { get; set; } = "";

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null) CurrentUserName = user.UserName;
        }

        public async Task<IActionResult> OnPostChangePasswordAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (!string.IsNullOrEmpty(Input.OldPassword) && !string.IsNullOrEmpty(Input.NewPassword))
            {
                var result = await _userManager.ChangePasswordAsync(user, Input.OldPassword, Input.NewPassword);
                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
                    CurrentUserName = user.UserName;
                    return Page();
                }
                await _signInManager.RefreshSignInAsync(user);
                TempData["Message"] = "Пароль изменен";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostChangeNameAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (!string.IsNullOrEmpty(Input.NewUserName) && Input.NewUserName != user.UserName)
            {
                var result = await _userManager.SetUserNameAsync(user, Input.NewUserName);
                if (result.Succeeded)
                {
                    await _signInManager.RefreshSignInAsync(user);
                    TempData["Message"] = "Логин изменен";
                }
                else
                {
                    foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return RedirectToPage();
        }
    }
}
