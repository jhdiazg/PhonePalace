
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PhonePalace.Infrastructure.Data
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            // --- Obtener los servicios necesarios ---
            var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // --- Crear Roles ---
            string[] roleNames = { "Admin", "Vendedor", "Almacenista", "Cajero" };
            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    // Crea el rol si no existe
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // --- Crear el Usuario Administrador ---
            var adminUser = await userManager.FindByEmailAsync("admin@phonepalace.com");
            if (adminUser == null)
            {
                var newAdminUser = new IdentityUser
                {
                    UserName = "admin@phonepalace.com",
                    Email = "admin@phonepalace.com",
                    EmailConfirmed = true // Confirma el email para poder hacer login directamente
                };

                // ¡IMPORTANTE! Usa una contraseña segura y gestiónala con User Secrets en desarrollo.
                // Esto es solo un ejemplo.
                var result = await userManager.CreateAsync(newAdminUser, "AdminPass123!");

                if (result.Succeeded)
                {
                    // Asigna el rol "Admin" al nuevo usuario
                    await userManager.AddToRoleAsync(newAdminUser, "Admin");
                }
            }
        }
    }
}
