using PhonePalace.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.ViewModels;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Administrador,Vendedor")]
    public class ClientsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public ClientsController(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        // GET: Clients
        public async Task<IActionResult> Index(string searchString, int? pageNumber, int? pageSize)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["PageSize"] = pageSize ?? 10;

            var clientsQuery = _context.Clients.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                clientsQuery = clientsQuery.Where(c =>
                    (c is NaturalPerson && (
                        ((NaturalPerson)c).FirstName.Contains(searchString) ||
                        ((NaturalPerson)c).LastName.Contains(searchString) ||
                        ((NaturalPerson)c).DocumentNumber.Contains(searchString) ||
                        (((NaturalPerson)c).FirstName + " " + ((NaturalPerson)c).LastName).Contains(searchString))) ||
                    (c is LegalEntity && (
                        ((LegalEntity)c).CompanyName.Contains(searchString) ||
                        ((LegalEntity)c).NIT.Contains(searchString))));
            }

            var viewModelQuery = clientsQuery
                .AsNoTracking()
                .Select(c => new ClientIndexViewModel
                {
                    ClientID = c.ClientID,
                    ClientType = c is NaturalPerson ? "Persona Natural" : "Persona Jurídica",
                    DisplayName = c is NaturalPerson ? ((NaturalPerson)c).FirstName + " " + ((NaturalPerson)c).LastName : (c is LegalEntity ? ((LegalEntity)c).CompanyName : ""),
                    Document = c is NaturalPerson ? ((NaturalPerson)c).DocumentNumber ?? "" : (c is LegalEntity ? ((LegalEntity)c).NIT ?? "" : ""),
                    Email = c.Email,
                    PhoneNumber = c.PhoneNumber,
                    IsActive = c.IsActive
                })
                .OrderBy(c => c.IsActive)
                .ThenBy(c => c.DisplayName);
            
            var paginated = await PaginatedList<ClientIndexViewModel>.CreateAsync(viewModelQuery, pageNumber ?? 1, pageSize ?? 10);
            return View(paginated);
        }

        // GET: Clients/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var client = await _context.Clients
                .Include(c => c.Department)
                .Include(c => c.Municipality)
                .AsNoTracking() // Mejora de rendimiento: no se necesita seguimiento para una vista de solo lectura.
                .FirstOrDefaultAsync(m => m.ClientID == id);

            if (client == null) return NotFound();

            return View(client);
        }

        // GET: Clients/Create
        public async Task<IActionResult> Create() // Corregido a async Task
        {
            await PopulateDropdowns(); // Corregido a await
            return View(new ClientCreateViewModel());
        }

        // POST: Clients/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClientCreateViewModel viewModel)
        {
            if (viewModel.ClientType == ClientTypeSelection.LegalEntity)
            {
                if (!string.IsNullOrEmpty(viewModel.NitNumber) && !string.IsNullOrEmpty(viewModel.VerificationDigit))
                {
                    var calculatedDv = ValidationHelper.CalculateNitVerificationDigit(viewModel.NitNumber);
                    if (calculatedDv < 0 || calculatedDv.ToString() != viewModel.VerificationDigit)
                    {
                        ModelState.AddModelError("VerificationDigit", "El dígito de verificación no es válido para el NIT proporcionado.");
                    }
                }
            }

            // Permitir valores nulos para campos opcionales
            if (string.IsNullOrWhiteSpace(viewModel.Email))
            {
                ModelState.Remove("Email");
            }
            if (viewModel.ClientType == ClientTypeSelection.NaturalPerson && string.IsNullOrWhiteSpace(viewModel.DocumentNumber))
            {
                ModelState.Remove("DocumentNumber");
            }
            if (viewModel.ClientType == ClientTypeSelection.LegalEntity && string.IsNullOrWhiteSpace(viewModel.NitNumber))
            {
                ModelState.Remove("NitNumber");
                ModelState.Remove("VerificationDigit");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(viewModel.DepartmentID, viewModel.MunicipalityID); // Corregido
                return View(viewModel);
            }

            Client newClient;

            if (viewModel.ClientType == ClientTypeSelection.NaturalPerson)
            {
                if (!string.IsNullOrWhiteSpace(viewModel.DocumentNumber) && await _context.NaturalPersons.AnyAsync(c => c.DocumentNumber == viewModel.DocumentNumber))
                {
                    ModelState.AddModelError("DocumentNumber", "Este número de documento ya está registrado.");
                    await PopulateDropdowns(viewModel.DepartmentID, viewModel.MunicipalityID);
                    return View(viewModel);
                }
                newClient = new NaturalPerson
                {
                    FirstName = viewModel.FirstName!.ToUpper(),
                    LastName = viewModel.LastName!.ToUpper(),
                    DocumentType = viewModel.DocumentType!.Value,
                    DocumentNumber = string.IsNullOrWhiteSpace(viewModel.DocumentNumber) ? viewModel.DocumentNumber.ToUpper() : null
                };
            }
            else if (viewModel.ClientType == ClientTypeSelection.LegalEntity)
            {
                var fullNit = $"{viewModel.NitNumber}-{viewModel.VerificationDigit}";
                if (!string.IsNullOrWhiteSpace(viewModel.NitNumber) && await _context.LegalEntities.AnyAsync(c => c.NIT == fullNit))
                {
                    ModelState.AddModelError("NitNumber", "Este NIT ya está registrado.");
                    await PopulateDropdowns(viewModel.DepartmentID, viewModel.MunicipalityID);
                    return View(viewModel);
                }
                newClient = new LegalEntity
                {
                    CompanyName = viewModel.CompanyName!.ToUpper(),
                    NIT = string.IsNullOrWhiteSpace(viewModel.NitNumber) ? fullNit.ToUpper() : null
                };
            }
            else
            {
                // This case should nopr hit if the view is correct, but it handles invalid states.
                ModelState.AddModelError("ClientType", "Debe seleccionar un tipo de cliente válido.");
                await PopulateDropdowns(viewModel.DepartmentID, viewModel.MunicipalityID);
                return View(viewModel);
            }

            // Asignar propiedades comunes
            newClient.Email = viewModel.Email;
            newClient.PhoneNumber = viewModel.PhoneNumber;
            newClient.DepartmentID = viewModel.DepartmentID;
            newClient.MunicipalityID = viewModel.MunicipalityID;
            newClient.StreetAddress = viewModel.StreetAddress?.ToUpper();
            newClient.IsActive = true;

            _context.Add(newClient);
            await _context.SaveChangesAsync();
            TempData["success"] = "Cliente creado exitosamente."; // Añadido
            await _auditService.LogAsync("Clientes", $"Creó el cliente '{newClient.DisplayName}' (ID: {newClient.ClientID}).");
            return RedirectToAction(nameof(Index));
        }

        // GET: Clients/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var client = await _context.Clients.FindAsync(id);
            if (client == null) return NotFound();


            if (client is NaturalPerson naturalPerson)
            {
                var viewModel = new NaturalPersonEditViewModel
                {
                    ClientID = naturalPerson.ClientID,
                    FirstName = naturalPerson.FirstName,
                    LastName = naturalPerson.LastName,
                    DocumentType = naturalPerson.DocumentType,
                    DocumentNumber = naturalPerson.DocumentNumber,
                    Email = naturalPerson.Email,
                    PhoneNumber = naturalPerson.PhoneNumber,
                    DepartmentID = naturalPerson.DepartmentID,
                    MunicipalityID = naturalPerson.MunicipalityID,
                    StreetAddress = naturalPerson.StreetAddress,
                    IsActive = naturalPerson.IsActive
                };
                await PopulateDropdowns(naturalPerson.DepartmentID, naturalPerson.MunicipalityID);
                return View("EditNaturalPerson", viewModel);
            }

            if (client is LegalEntity legalEntity)
            {
                var nitParts = legalEntity.NIT?.Split('-');
                var viewModel = new LegalEntityEditViewModel
                {
                    ClientID = legalEntity.ClientID,
                    CompanyName = legalEntity.CompanyName,
                    NitNumber = nitParts?.Length > 0 ? nitParts[0] : legalEntity.NIT,
                    VerificationDigit = nitParts?.Length > 1 ? nitParts[1] : null,
                    Email = legalEntity.Email,
                    PhoneNumber = legalEntity.PhoneNumber,
                    DepartmentID = legalEntity.DepartmentID,
                    MunicipalityID = legalEntity.MunicipalityID,
                    StreetAddress = legalEntity.StreetAddress,
                    IsActive = legalEntity.IsActive
                };
                await PopulateDropdowns(legalEntity.DepartmentID, legalEntity.MunicipalityID);
                return View("EditLegalEntity", viewModel);
            }

            return NotFound();
        }

        // POST: Clients/EditNaturalPerson/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditNaturalPerson(int id, NaturalPersonEditViewModel viewModel)
        {
            if (id != viewModel.ClientID) return NotFound();

            // Permitir valores nulos para DocumentNumber y Email
            if (string.IsNullOrWhiteSpace(viewModel.DocumentNumber)) ModelState.Remove("DocumentNumber");
            if (string.IsNullOrWhiteSpace(viewModel.Email)) ModelState.Remove("Email");

            // Validar que el número de documento sea único, excluyendo al cliente actual.
            if (!string.IsNullOrWhiteSpace(viewModel.DocumentNumber) && await _context.NaturalPersons.AnyAsync(c => c.DocumentNumber == viewModel.DocumentNumber && c.ClientID != id))
            {
                ModelState.AddModelError("DocumentNumber", "Este número de documento ya está registrado para otro cliente.");
            }

            if (ModelState.IsValid)
            {
                var clientToUpdate = await _context.NaturalPersons.FindAsync(id); // Simplificado
                if (clientToUpdate == null) return NotFound();

                clientToUpdate.FirstName = viewModel.FirstName?.ToUpper() ?? string.Empty;
                clientToUpdate.LastName = viewModel.LastName?.ToUpper() ?? string.Empty;
                clientToUpdate.DocumentType = viewModel.DocumentType;
                clientToUpdate.DocumentNumber = string.IsNullOrWhiteSpace(viewModel.DocumentNumber) ? viewModel.DocumentNumber.ToUpper() : null;
                clientToUpdate.Email = viewModel.Email;
                clientToUpdate.PhoneNumber = viewModel.PhoneNumber;
                clientToUpdate.DepartmentID = viewModel.DepartmentID;
                clientToUpdate.MunicipalityID = viewModel.MunicipalityID;
                clientToUpdate.StreetAddress = viewModel.StreetAddress?.ToUpper(); // Corregido
                clientToUpdate.IsActive = viewModel.IsActive;

                try
                {
                    _context.Update(clientToUpdate);
                    await _context.SaveChangesAsync();
                    TempData["success"] = "Cliente natural actualizado exitosamente."; // Añadido
                    await _auditService.LogAsync("Clientes", $"Editó el cliente '{clientToUpdate.DisplayName}' (ID: {clientToUpdate.ClientID}).");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ClientExists(viewModel.ClientID)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            await PopulateDropdowns(viewModel.DepartmentID, viewModel.MunicipalityID); // Corregido a await
            return View("EditNaturalPerson", viewModel); // Corregido
        }

        // POST: Clients/EditLegalEntity/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLegalEntity(int id, LegalEntityEditViewModel viewModel)
        {
            if (id != viewModel.ClientID) return NotFound();

            // Permitir valores nulos para NIT y Email
            if (string.IsNullOrWhiteSpace(viewModel.NitNumber))
            {
                ModelState.Remove("NitNumber");
                ModelState.Remove("VerificationDigit");
            }
            if (string.IsNullOrWhiteSpace(viewModel.Email)) ModelState.Remove("Email");

            if (!string.IsNullOrWhiteSpace(viewModel.NitNumber) && !string.IsNullOrWhiteSpace(viewModel.VerificationDigit))
            {
                var calculatedDv = ValidationHelper.CalculateNitVerificationDigit(viewModel.NitNumber);
                if (calculatedDv < 0 || calculatedDv.ToString() != viewModel.VerificationDigit)
                {
                    ModelState.AddModelError("VerificationDigit", "El dígito de verificación no es válido para el NIT proporcionado.");
                }
            }

            var fullNit = $"{viewModel.NitNumber}-{viewModel.VerificationDigit}";

            // Validar que el NIT sea único, excluyendo al cliente actual.
            if (!string.IsNullOrWhiteSpace(viewModel.NitNumber) && await _context.LegalEntities.AnyAsync(c => c.NIT == fullNit && c.ClientID != id))
            {
                ModelState.AddModelError("NitNumber", "Este NIT ya está registrado para otro cliente.");
            }


            if (ModelState.IsValid)
            {
                try
                {
                    var clientToUpdate = await _context.LegalEntities.FindAsync(id);
                    if (clientToUpdate == null) return NotFound();

                    clientToUpdate.CompanyName = (viewModel.CompanyName ?? string.Empty).ToUpper();
                    clientToUpdate.NIT = string.IsNullOrWhiteSpace(viewModel.NitNumber) ? fullNit.ToUpper() : null;
                    clientToUpdate.Email = viewModel.Email;
                    clientToUpdate.PhoneNumber = viewModel.PhoneNumber;
                    clientToUpdate.DepartmentID = viewModel.DepartmentID;
                    clientToUpdate.MunicipalityID = viewModel.MunicipalityID;
                    clientToUpdate.StreetAddress = viewModel.StreetAddress?.ToUpper();

                    clientToUpdate.IsActive = viewModel.IsActive;

                    _context.Update(clientToUpdate);
                    await _context.SaveChangesAsync();
                    TempData["success"] = "Cliente jurídico actualizado exitosamente.";
                    await _auditService.LogAsync("Clientes", $"Editó el cliente '{clientToUpdate.DisplayName}' (ID: {clientToUpdate.ClientID}).");
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ClientExists(viewModel.ClientID)) return NotFound();
                    else throw;
                }
            }

            await PopulateDropdowns(viewModel.DepartmentID, viewModel.MunicipalityID);
            return View("EditLegalEntity", viewModel);
        }


        // GET: Clients/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var client = await _context.Clients
                .AsNoTracking() // Mejora de rendimiento: no se necesita seguimiento para la vista de eliminación.
                .FirstOrDefaultAsync(m => m.ClientID == id);

            if (client == null) return NotFound();

            return View(client);
        }

        // POST: Clients/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client != null)
            {
                var clientName = client.DisplayName;
                var clientId = client.ClientID;
                client.IsActive = false;
                _context.Update(client);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Clientes", $"Eliminó (desactivó) el cliente '{clientName}' (ID: {clientId}).");
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: /Clients/GetMunicipalities/5
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

        [HttpGet("api/clients/{id}/balance")]
        public async Task<IActionResult> GetBalance(int id)
        {
            var client = await _context.Clients
                .AsNoTracking()
                .Select(c => new { c.ClientID, c.Balance })
                .FirstOrDefaultAsync(c => c.ClientID == id);

            if (client == null) return NotFound();
            return Ok(new { balance = client.Balance });
        }

        private bool ClientExists(int id)
        {
            return _context.Clients.Any(e => e.ClientID == id);
        }

        private async Task PopulateDropdowns(string? departmentId = null, string? municipalityId = null)
        {
            var departments = await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
            ViewData["DepartmentID"] = new SelectList(departments, "DepartmentID", "Name", departmentId);

            if (!string.IsNullOrEmpty(departmentId))
            {
                var municipalities = await _context.Municipalities
                    .Where(m => m.DepartmentID == departmentId)
                    .AsNoTracking()
                    .OrderBy(m => m.Name)
                    .ToListAsync();
                ViewData["MunicipalityID"] = new SelectList(municipalities, "MunicipalityID", "Name", municipalityId);
            }
            else
            {
                ViewData["MunicipalityID"] = new SelectList(new List<SelectListItem> { new SelectListItem { Text = "Seleccione un departamento", Value = "" } }, "Value", "Text");
            }
        }
    }
}