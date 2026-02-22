using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using PhonePalace.Infrastructure.Configuration;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.ViewModels;
using System.Linq;
using System.Threading.Tasks;

namespace PhonePalace.Web.Controllers
{
    [AllowAnonymous]
    public class PublicAccessController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly CompanySettings _companySettings;

        public PublicAccessController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ApplicationDbContext context, IOptions<CompanySettings> companySettings)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _companySettings = companySettings.Value;
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (_signInManager.IsSignedIn(User))
            {
                return RedirectToAction("Index", "Home");
            }
            
            ViewData["DepartmentID"] = new SelectList(_context.Departments.OrderBy(d => d.Name), "DepartmentID", "Name");
            ViewData["CompanyName"] = _companySettings.CompanyName;
            
            return View(new PublicClientRegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(PublicClientRegisterViewModel model)
        {
            if (!model.DataAuthorization)
            {
                ModelState.AddModelError("DataAuthorization", "Debe autorizar el tratamiento de datos personales para continuar.");
            }

            if (ModelState.IsValid)
            {
                // 1. Crear el Usuario de Login (Identity)
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email, EmailConfirmed = true };
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // 2. Asignar Rol "Cliente"
                    await _userManager.AddToRoleAsync(user, "Cliente");

                    // 3. Crear la Entidad de Negocio (Client)
                    Client newClient;

                    if (model.ClientType == ClientTypeSelection.NaturalPerson)
                    {
                        newClient = new NaturalPerson
                        {
                            FirstName = model.FirstName?.ToUpper() ?? "N/A",
                            LastName = model.LastName?.ToUpper() ?? "N/A",
                            DocumentType = model.DocumentType ?? DocumentType.CitizenshipCard,
                            DocumentNumber = model.DocumentNumber ?? string.Empty
                        };
                    }
                    else
                    {
                        var fullNit = $"{model.NitNumber}-{model.VerificationDigit}";
                        newClient = new LegalEntity
                        {
                            CompanyName = model.CompanyName?.ToUpper() ?? "N/A",
                            NIT = fullNit
                        };
                    }

                    // Datos comunes
                    newClient.Email = model.Email;
                    newClient.PhoneNumber = model.PhoneNumber;
                    newClient.StreetAddress = model.Address;
                    newClient.DepartmentID = model.DepartmentID;
                    newClient.MunicipalityID = model.MunicipalityID;
                    newClient.IsActive = true;
                    // Nota: Idealmente agregarías una propiedad UserId a la entidad Client para enlazarlos fuertemente.
                    // Por ahora, el sistema los enlazará por Email cuando busques el cliente.

                    _context.Clients.Add(newClient);
                    await _context.SaveChangesAsync();

                    // 4. Iniciar sesión y redirigir
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("MyData", "ClientPortal");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            ViewData["DepartmentID"] = new SelectList(_context.Departments.OrderBy(d => d.Name), "DepartmentID", "Name", model.DepartmentID);
            ViewData["CompanyName"] = _companySettings.CompanyName;
            return View(model);
        }

        [HttpGet]
        public async Task<JsonResult> GetMunicipalities(string departmentId)
        {
            var municipalities = await _context.Municipalities
                .Where(m => m.DepartmentID == departmentId)
                .OrderBy(m => m.Name)
                .Select(m => new { value = m.MunicipalityID, text = m.Name })
                .ToListAsync();
            return Json(municipalities);
        }
    }
}
