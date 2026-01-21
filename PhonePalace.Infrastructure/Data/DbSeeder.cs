
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace PhonePalace.Infrastructure.Data
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            // --- Obtener los servicios necesarios ---
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            // No se puede usar ILogger<DbSeeder> porque DbSeeder es una clase estática.
            // En su lugar, usamos ILoggerFactory para crear un logger con un nombre de categoría específico.
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

            // --- Crear Roles ---
            string[] roleNames = { "Administrador", "Vendedor", "Almacenista", "Cajero" };
            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    // Crea el rol si no existe
                    var roleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
                    if (roleResult.Succeeded)
                    {
                        logger.LogInformation("Rol '{RoleName}' creado exitosamente.", roleName);
                    }
                    else
                    {
                        logger.LogError("Error creando el rol '{RoleName}'.", roleName);
                    }
                }
            }

            // --- Crear el Usuario Administrador ---
            var adminUser = await userManager.FindByEmailAsync("admin@phonepalace.com");
            if (adminUser == null)
            {
                var newAdminUser = new ApplicationUser
                {
                    UserName = "admin@phonepalace.com",
                    Email = "admin@phonepalace.com",
                    EmailConfirmed = true // Confirma el email para poder hacer login directamente
                };

                // ¡IMPORTANTE! Usa una contraseña segura y gestiónala con User Secrets en desarrollo.
                // Esto es solo un ejemplo.
                var createResult = await userManager.CreateAsync(newAdminUser, "AdminPass123!");

                if (createResult.Succeeded)
                {
                    logger.LogInformation("Usuario administrador creado exitosamente.");
                    // Asigna el rol "Administrador" al nuevo usuario
                    var addToRoleResult = await userManager.AddToRoleAsync(newAdminUser, "Administrador");
                    if (addToRoleResult.Succeeded)
                    {
                        logger.LogInformation("Rol 'Administrador' asignado al usuario administrador.");
                    }
                    else
                    {
                        foreach (var error in addToRoleResult.Errors)
                        {
                            logger.LogError("Error asignando rol 'Administrador': {Code} - {Description}", error.Code, error.Description);
                        }
                    }
                }
                else
                {
                    foreach (var error in createResult.Errors)
                    {
                        logger.LogError("Error creando usuario administrador: {Code} - {Description}", error.Code, error.Description);
                    }
                }
            }
        }
    }
}
