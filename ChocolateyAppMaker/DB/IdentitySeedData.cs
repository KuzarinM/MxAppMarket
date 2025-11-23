using Microsoft.AspNetCore.Identity;

namespace ChocolateyAppMaker.Data
{
    public static class IdentitySeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // 1. Создаем роли
            string[] roleNames = { "Admin", "User" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Создаем дефолтного админа, если его нет
            if (await userManager.FindByNameAsync("admin") == null)
            {
                var adminUser = new IdentityUser
                {
                    UserName = "admin",
                    Email = "admin@local.host",
                    EmailConfirmed = true
                };

                // Пароль 'admin' - Identity требует спецсимволы по умолчанию, 
                // но мы ослабим требования в Program.cs
                var result = await userManager.CreateAsync(adminUser, "admin");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }
    }
}
