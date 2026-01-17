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
    [Authorize(Roles = "Administrador")]
    public class UserManagementController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAuditService _auditService;

        public UserManagementController(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditService auditService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _auditService = auditService;
        }

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

        public IActionResult Create()
        {
            var model = new CreateUserViewModel
            {
                AvailableRoles = new List<string> { "Administrador", "Cajero", "Almacenista", "Vendedor" }
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.AvailableRoles = new List<string> { "Administrador", "Cajero", "Almacenista", "Vendedor" };
                return View(model);
            }

            var user = new IdentityUser
            {
                UserName = model.Email,
                Email = model.Email,
                EmailConfirmed = true // Para simplificar, confirmar email automáticamente
            };

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

            model.AvailableRoles = new List<string> { "Administrador", "Cajero", "Almacenista", "Vendedor" };
            return View(model);
        }

        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var definedRoles = new List<string> { "Administrador", "Cajero", "Almacenista", "Vendedor" };
            var roles = await _roleManager.Roles.Where(r => r.Name != null && definedRoles.Contains(r.Name)).ToListAsync();
            var userRoles = await _userManager.GetRolesAsync(user);

            var model = new EditUserRolesViewModel
            {
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                AvailableRoles = roles.Select(r => r.Name).Where(n => n != null).Cast<string>().ToList(),
                SelectedRoles = userRoles.ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserRolesViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                return NotFound();
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var rolesToAdd = model.SelectedRoles.Except(currentRoles).ToList();
            var rolesToRemove = currentRoles.Except(model.SelectedRoles).ToList();

            if (rolesToAdd.Any())
            {
                var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
                if (!addResult.Succeeded)
                {
                    ModelState.AddModelError("", "Error al agregar roles.");
                    return View(model);
                }
            }

            if (rolesToRemove.Any())
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                if (!removeResult.Succeeded)
                {
                    ModelState.AddModelError("", "Error al remover roles.");
                    return View(model);
                }
            }

            // Log de auditoría
            await _auditService.LogAsync("Usuarios", $"Roles actualizados para usuario {user.UserName}: Agregados {string.Join(", ", rolesToAdd)}, Removidos {string.Join(", ", rolesToRemove)}");

            TempData["Success"] = "Roles actualizados exitosamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
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
    }
}