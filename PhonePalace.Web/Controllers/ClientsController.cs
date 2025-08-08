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
    [Authorize(Roles = "Admin,Vendedor")]
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
        public async Task<IActionResult> Index(int? pageNumber)
        {
            var clientsQuery = _context.Clients
                .Where(c => c.IsActive)
                .AsNoTracking()
                .Select(c => new ClientIndexViewModel
                {
                    ClientID = c.ClientID,
                    ClientType = c is NaturalPerson ? "Persona Natural" : "Persona Jurídica",
                    DisplayName = c.DisplayName,
                    Document = c is NaturalPerson ? ((NaturalPerson)c).DocumentNumber : ((LegalEntity)c).NIT,
                    Email = c.Email,
                    PhoneNumber = c.PhoneNumber
                });

            int pageSize = 10;
            return View(await PaginatedList<ClientIndexViewModel>.CreateAsync(clientsQuery, pageNumber ?? 1, pageSize));
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
            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(viewModel.DepartmentID, viewModel.MunicipalityID); // Corregido
                return View(viewModel);
            }

            Client newClient;

            if (viewModel.ClientType == ClientTypeSelection.NaturalPerson)
            {
                if (await _context.NaturalPersons.AnyAsync(c => c.DocumentNumber == viewModel.DocumentNumber!))
                {
                    ModelState.AddModelError("DocumentNumber", "Este número de documento ya está registrado.");
                    await PopulateDropdowns(viewModel.DepartmentID, viewModel.MunicipalityID);
                    return View(viewModel);
                }
                newClient = new NaturalPerson
                {
                    FirstName = viewModel.FirstName!,
                    LastName = viewModel.LastName!,
                    DocumentType = viewModel.DocumentType!.Value,
                    DocumentNumber = viewModel.DocumentNumber!
                };
            }
            else if (viewModel.ClientType == ClientTypeSelection.LegalEntity)
            {
                if (await _context.LegalEntities.AnyAsync(c => c.NIT == viewModel.NIT!))
                {
                    ModelState.AddModelError("NIT", "Este NIT ya está registrado.");
                    await PopulateDropdowns(viewModel.DepartmentID, viewModel.MunicipalityID);
                    return View(viewModel);
                }
                newClient = new LegalEntity
                {
                    CompanyName = viewModel.CompanyName!,
                    NIT = viewModel.NIT!
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
            newClient.StreetAddress = viewModel.StreetAddress;
            newClient.IsActive = true;

            _context.Add(newClient);
            await _context.SaveChangesAsync();
            TempData["success"] = "Cliente creado exitosamente."; // Añadido
            await _auditService.LogAsync("Clients", $"Creó el cliente '{newClient.DisplayName}' (ID: {newClient.ClientID}).");
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
                var viewModel = new LegalEntityEditViewModel
                {
                    ClientID = legalEntity.ClientID,
                    CompanyName = legalEntity.CompanyName,
                    NIT = legalEntity.NIT,
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

            // Validar que el número de documento sea único, excluyendo al cliente actual.
            if (await _context.NaturalPersons.AnyAsync(c => c.DocumentNumber == viewModel.DocumentNumber && c.ClientID != id))
            {
                ModelState.AddModelError("DocumentNumber", "Este número de documento ya está registrado para otro cliente.");
            }

            if (ModelState.IsValid)
            {
                var clientToUpdate = await _context.NaturalPersons.FindAsync(id); // Simplificado
                if (clientToUpdate == null) return NotFound();

                clientToUpdate.FirstName = viewModel.FirstName;
                clientToUpdate.LastName = viewModel.LastName;
                clientToUpdate.DocumentType = viewModel.DocumentType;
                clientToUpdate.DocumentNumber = viewModel.DocumentNumber;
                clientToUpdate.Email = viewModel.Email;
                clientToUpdate.PhoneNumber = viewModel.PhoneNumber;
                clientToUpdate.DepartmentID = viewModel.DepartmentID;
                clientToUpdate.MunicipalityID = viewModel.MunicipalityID;
                clientToUpdate.StreetAddress = viewModel.StreetAddress; // Corregido
                clientToUpdate.IsActive = viewModel.IsActive;

                try
                {
                    _context.Update(clientToUpdate);
                    await _context.SaveChangesAsync();
                    TempData["success"] = "Cliente natural actualizado exitosamente."; // Añadido
                    await _auditService.LogAsync("Clients", $"Editó el cliente '{clientToUpdate.DisplayName}' (ID: {clientToUpdate.ClientID}).");
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

            // Validar que el RUC sea único, excluyendo al cliente actual.
            if (await _context.LegalEntities.AnyAsync(c => c.NIT == viewModel.NIT && c.ClientID != id))
            {
                ModelState.AddModelError("NIT", "Este NIT ya está registrado para otro cliente.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var clientToUpdate = await _context.LegalEntities.FindAsync(id);
                    if (clientToUpdate == null) return NotFound();

                    clientToUpdate.CompanyName = viewModel.CompanyName;
                    clientToUpdate.NIT = viewModel.NIT;
                    clientToUpdate.Email = viewModel.Email;
                    clientToUpdate.PhoneNumber = viewModel.PhoneNumber;
                    clientToUpdate.DepartmentID = viewModel.DepartmentID;
                    clientToUpdate.MunicipalityID = viewModel.MunicipalityID;
                    clientToUpdate.StreetAddress = viewModel.StreetAddress;

                    clientToUpdate.IsActive = viewModel.IsActive;

                    _context.Update(clientToUpdate);
                    await _context.SaveChangesAsync();
                    TempData["success"] = "Cliente jurídico actualizado exitosamente.";
                    await _auditService.LogAsync("Clients", $"Editó el cliente '{clientToUpdate.DisplayName}' (ID: {clientToUpdate.ClientID}).");
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
                await _auditService.LogAsync("Clients", $"Eliminó (desactivó) el cliente '{clientName}' (ID: {clientId}).");
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