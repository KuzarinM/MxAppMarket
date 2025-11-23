using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ChocolateyAppMaker.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class UsersModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;

        public UsersModel(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        public List<UserViewModel> Users { get; set; } = new();

        [BindProperty]
        public CreateUserModel NewUser { get; set; } = new();

        [BindProperty]
        public EditUserModel EditUser { get; set; } = new();

        // ... Классы моделей (UserViewModel, CreateUserModel, EditUserModel) оставляем как были ...
        public class UserViewModel
        {
            public string Id { get; set; }
            public string UserName { get; set; }
            public bool IsAdmin { get; set; }
            public bool IsLocked { get; set; }
        }

        public class CreateUserModel
        {
            [Required(ErrorMessage = "Логин обязателен")]
            public string UserName { get; set; } = "";

            [Required(ErrorMessage = "Пароль обязателен")]
            public string Password { get; set; } = "";

            public bool IsAdmin { get; set; }
        }

        public class EditUserModel
        {
            [Required] public string Id { get; set; } = "";
            public bool IsAdmin { get; set; }
            public bool IsLocked { get; set; }
        }

        public async Task OnGetAsync()
        {
            await LoadUsersList();
        }

        // --- ИСПРАВЛЕННЫЙ МЕТОД СОЗДАНИЯ ---
        public async Task<IActionResult> OnPostCreateAsync()
        {
            // 1. Сбрасываем все ошибки валидации (включая ошибки пустой формы редактирования)
            ModelState.Clear();

            // 2. Принудительно проверяем только модель NewUser
            if (!TryValidateModel(NewUser, nameof(NewUser)))
            {
                // Если есть ошибки именно в NewUser, показываем их
                await LoadUsersList();
                ViewData["ShowCreateModal"] = true;
                return Page();
            }

            // Логика создания
            var user = new IdentityUser
            {
                UserName = NewUser.UserName,
                Email = $"{NewUser.UserName}@local.host",
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, NewUser.Password);

            if (result.Succeeded)
            {
                var role = NewUser.IsAdmin ? "Admin" : "User";
                await _userManager.AddToRoleAsync(user, role);
                TempData["Message"] = $"Пользователь {NewUser.UserName} создан";
                return RedirectToPage();
            }

            // Ошибки Identity (например, такой юзер уже есть)
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await LoadUsersList();
            ViewData["ShowCreateModal"] = true;
            return Page();
        }

        // --- ИСПРАВЛЕННЫЙ МЕТОД РЕДАКТИРОВАНИЯ ---
        public async Task<IActionResult> OnPostEditAsync()
        {
            // 1. Сбрасываем ошибки (включая ошибки пустой формы создания)
            ModelState.Clear();

            // 2. Проверяем только модель EditUser
            if (!TryValidateModel(EditUser, nameof(EditUser)))
            {
                return RedirectToPage(); // Если ID пустой (хак), просто обновляем страницу
            }

            var user = await _userManager.FindByIdAsync(EditUser.Id);
            if (user == null) return NotFound();

            if (user.UserName == User.Identity.Name)
            {
                TempData["Error"] = "Нельзя изменять собственные права";
                return RedirectToPage();
            }

            // Блокировка
            if (EditUser.IsLocked)
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            else
                await _userManager.SetLockoutEndDateAsync(user, null);

            // Роли
            var currentRoles = await _userManager.GetRolesAsync(user);
            var targetRole = EditUser.IsAdmin ? "Admin" : "User";

            if (!currentRoles.Contains(targetRole))
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, targetRole);
            }

            TempData["Message"] = $"Пользователь {user.UserName} обновлен";
            return RedirectToPage();
        }

        // Удаление
        public async Task<IActionResult> OnPostDeleteAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null && user.UserName != User.Identity.Name)
            {
                await _userManager.DeleteAsync(user);
                TempData["Message"] = "Пользователь удален";
            }
            return RedirectToPage();
        }

        private async Task LoadUsersList()
        {
            Users.Clear();
            var users = await _userManager.Users.ToListAsync();
            foreach (var u in users)
            {
                Users.Add(new UserViewModel
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    IsAdmin = await _userManager.IsInRoleAsync(u, "Admin"),
                    IsLocked = await _userManager.IsLockedOutAsync(u)
                });
            }
        }
    }
}
