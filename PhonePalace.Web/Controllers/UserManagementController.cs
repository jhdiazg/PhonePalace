using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Web.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PhonePalace.Web.Controllers
{
    [Authorize]
    public class UserManagementController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAuditService _auditService;
        private readonly IFileStorageService _fileStorageService;
        private readonly List<string> _definedRoles = new List<string> { "Administrador", "Cajero", "Almacenista", "Vendedor" };

        public UserManagementController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IFileStorageService fileStorageService,
            IAuditService auditService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _auditService = auditService;
            _fileStorageService = fileStorageService;
        }

        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var userRolesViewModel = new List<UserRolesViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userRolesViewModel.Add(new UserRolesViewModel
                {
                    UserId = user.Id,
                    UserName = user.UserName?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    Roles = roles.ToList()
                });
            }

            return View(userRolesViewModel);
        }

        [Authorize(Roles = "Administrador")]
        public IActionResult Create()
        {
            var model = new CreateUserViewModel
            {
                AvailableRoles = _definedRoles
            };
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Administrador")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.AvailableRoles = _definedRoles;
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                EmailConfirmed = true // Para simplificar, confirmar email automáticamente
            };

            // Handle profile picture upload
            if (model.ProfilePictureFile != null)
            {
                user.ProfilePictureUrl = await _fileStorageService.SaveFileAsync(model.ProfilePictureFile, "users");
            }

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                // Asignar roles seleccionados
                if (model.SelectedRoles != null && model.SelectedRoles.Any())
                {
                    await _userManager.AddToRolesAsync(user, model.SelectedRoles);
                }

                await _auditService.LogAsync("Usuarios", $"Usuario {user.UserName} creado con roles: {string.Join(", ", model.SelectedRoles ?? new List<string>())}");

                TempData["Success"] = "Usuario creado exitosamente.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            model.AvailableRoles = _definedRoles;
            return View(model);
        }

        public async Task<IActionResult> Edit(string id)
        {
            var currentUserId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Administrador");

            // Seguridad: Solo permitir si es Admin o si el usuario se edita a sí mismo
            if (!isAdmin && currentUserId != id)
            {
                return Forbid();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _roleManager.Roles.Where(r => r.Name != null && _definedRoles.Contains(r.Name)).ToListAsync();
            var userRoles = await _userManager.GetRolesAsync(user);

            var model = new UserEditViewModel
            {
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                ProfilePictureUrl = user.ProfilePictureUrl,
                // Solo mostrar roles disponibles si es Admin
                AvailableRoles = isAdmin ? roles.Select(r => r.Name).Where(n => n != null).Cast<string>().ToList() : new List<string>(),
                SelectedRoles = userRoles.ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserEditViewModel model)
        {
            var currentUserId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Administrador");

            // Seguridad: Solo permitir si es Admin o si el usuario se edita a sí mismo
            if (!isAdmin && currentUserId != model.UserId)
            {
                return Forbid();
            }

            // Recargar roles disponibles en caso de que tengamos que devolver la vista por error
            if (isAdmin)
            {
                var allRoles = await _roleManager.Roles.Where(r => r.Name != null && _definedRoles.Contains(r.Name)).ToListAsync();
                model.AvailableRoles = allRoles.Select(r => r.Name).Where(n => n != null).Cast<string>().ToList();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                return NotFound();
            }

            var changes = new List<string>();
            bool hasChanges = false;

            // 1. Actualizar Nombre de Usuario (Solo en memoria)
            if (model.UserName != user.UserName)
            {
                changes.Add($"Nombre de usuario cambiado de '{user.UserName}' a '{model.UserName}'");
                user.UserName = model.UserName;
                // Importante: Actualizar el nombre normalizado para que el login funcione
                await _userManager.UpdateNormalizedUserNameAsync(user);
                hasChanges = true;
            }

            // 2. Actualizar Teléfono (Solo en memoria)
            if (model.PhoneNumber != user.PhoneNumber)
            {
                changes.Add($"Teléfono cambiado de '{user.PhoneNumber ?? "N/A"}' a '{model.PhoneNumber ?? "N/A"}'");
                user.PhoneNumber = model.PhoneNumber;
                hasChanges = true;
            }

            // 3. Actualizar Foto de Perfil (Solo en memoria)
            if (model.ProfilePictureFile != null)
            {
                changes.Add("Foto de perfil actualizada");
                if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
                {
                    await _fileStorageService.DeleteFileAsync(user.ProfilePictureUrl);
                }
                user.ProfilePictureUrl = await _fileStorageService.SaveFileAsync(model.ProfilePictureFile, "users");
                hasChanges = true;
            }

            // 4. GUARDAR TODO EN UNA SOLA TRANSACCIÓN
            if (hasChanges)
            {
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    foreach (var error in updateResult.Errors) ModelState.AddModelError("", error.Description);
                    return View(model);
                }
            }

            // Solo el administrador puede cambiar roles
            if (isAdmin)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                var rolesToAdd = model.SelectedRoles.Except(currentRoles).ToList();
                var rolesToRemove = currentRoles.Except(model.SelectedRoles).ToList();

                if (rolesToAdd.Any())
                {
                    changes.Add($"Roles agregados: {string.Join(", ", rolesToAdd)}");
                    var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
                    if (!addResult.Succeeded)
                    {
                        ModelState.AddModelError("", "Error al agregar roles.");
                        return View(model);
                    }
                }

                if (rolesToRemove.Any())
                {
                    changes.Add($"Roles removidos: {string.Join(", ", rolesToRemove)}");
                    var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                    if (!removeResult.Succeeded)
                    {
                        ModelState.AddModelError("", "Error al remover roles.");
                        return View(model);
                    }
                }
            }

            // Log de auditoría
            if (changes.Any())
            {
                await _auditService.LogAsync("Usuarios", $"Usuario {user.UserName} actualizado: {string.Join("; ", changes)}");
            }

            TempData["Success"] = "Usuario actualizado exitosamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Administrador")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.Now.AddYears(100));
            await _auditService.LogAsync("Usuarios", $"Usuario {user.UserName} bloqueado");

            TempData["Success"] = "Usuario bloqueado exitosamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Administrador")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            await _userManager.SetLockoutEndDateAsync(user, null);
            await _auditService.LogAsync("Usuarios", $"Usuario {user.UserName} desbloqueado");

            TempData["Success"] = "Usuario desbloqueado exitosamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();
            return RedirectToAction(nameof(Edit), new { id = user.Id });
        }
    }
}