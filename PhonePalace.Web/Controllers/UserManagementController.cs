using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class UserManagementController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserManagementController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var userRolesViewModel = new List<UserRolesViewModel>();
            foreach (ApplicationUser user in users)
            {
                var thisViewModel = new UserRolesViewModel
                {
                    UserId = user.Id,
                    Email = user.Email,
                    UserName = user.UserName,
                    Roles = await GetUserRoles(user),
                    IsLocked = await _userManager.IsLockedOutAsync(user)
                };
                userRolesViewModel.Add(thisViewModel);
            }
            return View(userRolesViewModel);
        }

        private async Task<List<string>> GetUserRoles(ApplicationUser user)
        {
            return new List<string>(await _userManager.GetRolesAsync(user));
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

            // Bloquear al usuario hasta una fecha en el futuro lejano
            var result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

            if (result.Succeeded)
            {
                TempData["Success"] = $"El usuario {user.UserName} ha sido bloqueado.";
            }
            else
            {
                TempData["Error"] = "Error al bloquear el usuario.";
            }

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

            // Desbloquear al usuario estableciendo la fecha de bloqueo en el pasado
            var result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.Now);

            if (result.Succeeded)
            {
                TempData["Success"] = $"El usuario {user.UserName} ha sido desbloqueado.";
            }
            else
            {
                TempData["Error"] = "Error al desbloquear el usuario.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}