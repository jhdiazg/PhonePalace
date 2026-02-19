using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PhonePalace.Infrastructure.Data;
using System.Threading.Tasks;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Cliente")]
    public class ClientPortalController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ClientPortalController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> MyData()
        {
            var user = await _userManager.GetUserAsync(User);
            return View(user);
        }
    }
}