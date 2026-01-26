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
    public class SuppliersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public SuppliersController(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        // GET: Suppliers
        public async Task<IActionResult> Index(int? pageNumber, int? pageSize)
        {
            ViewData["PageSize"] = pageSize ?? 10;

            var suppliersQuery = _context.Suppliers
                .AsNoTracking()
                .Select(s => new SupplierIndexViewModel
                {
                    SupplierID = s.SupplierID,
                    SupplierType = s is NaturalPersonSupplier ? "Persona Natural" : "Persona Jurídica",
                    DisplayName = s.DisplayName,
                    Document = s is NaturalPersonSupplier ? ((NaturalPersonSupplier)s).DocumentNumber : ((LegalEntitySupplier)s).NIT ?? string.Empty,
                    Email = s.Email ?? string.Empty,
                    PhoneNumber = s.PhoneNumber ?? string.Empty,
                    IsActive = s.IsActive
                });

            return View(await PaginatedList<SupplierIndexViewModel>.CreateAsync(suppliersQuery, pageNumber ?? 1, pageSize ?? 10));
        }

        // GET: Suppliers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var supplier = await _context.Suppliers
                .Include(s => s.Department)
                .Include(s => s.Municipality)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.SupplierID == id);

            if (supplier == null) return NotFound();

            return View(supplier);
        }

        // GET: Suppliers/Create
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View(new SupplierCreateViewModel());
        }

        // POST: Suppliers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SupplierCreateViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(viewModel.DepartmentID, viewModel.MunicipalityID);
                return View(viewModel);
            }

            Supplier newSupplier;

            if (viewModel.SupplierType == SupplierTypeSelection.NaturalPerson)
            {
                if (await _context.NaturalPersonSuppliers.AnyAsync(s => s.DocumentNumber == viewModel.DocumentNumber!))
                {
                    ModelState.AddModelError("DocumentNumber", "Este número de documento ya está registrado.");
                    await PopulateDropdowns(viewModel.DepartmentID, viewModel.MunicipalityID);
                    return View(viewModel);
                }
                newSupplier = new NaturalPersonSupplier
                {
                    FirstName = viewModel.FirstName!.ToUpper(),
                    LastName = viewModel.LastName!.ToUpper(),
                    DocumentType = viewModel.DocumentType!.Value,
                    DocumentNumber = viewModel.DocumentNumber!.ToUpper()
                };
            }
            else if (viewModel.SupplierType == SupplierTypeSelection.LegalEntity)
            {
                if (!string.IsNullOrEmpty(viewModel.NitNumber) && !string.IsNullOrEmpty(viewModel.VerificationDigit))
                {
                    var calculatedDv = ValidationHelper.CalculateNitVerificationDigit(viewModel.NitNumber);
                    if (calculatedDv < 0 || calculatedDv.ToString() != viewModel.VerificationDigit)
                    {
                        ModelState.AddModelError("VerificationDigit", "El dígito de verificación no es válido para el NIT proporcionado.");
                    }
                }

                if (!ModelState.IsValid)
                {
                    await PopulateDropdowns(viewModel.DepartmentID, viewModel.MunicipalityID);
                    return View(viewModel);
                }

                var fullNit = $"{viewModel.NitNumber}-{viewModel.VerificationDigit}";
                if (await _context.LegalEntitySuppliers.AnyAsync(s => s.NIT == fullNit))
                {
                    ModelState.AddModelError("NitNumber", "Este NIT ya está registrado.");
                    await PopulateDropdowns(viewModel.DepartmentID, viewModel.MunicipalityID);
                    return View(viewModel);
                }
                newSupplier = new LegalEntitySupplier
                {
                    CompanyName = viewModel.CompanyName!.ToUpper(),
                    NIT = fullNit.ToUpper()
                };
            }
            else
            {
                ModelState.AddModelError("SupplierType", "Debe seleccionar un tipo de proveedor válido.");
                await PopulateDropdowns(viewModel.DepartmentID, viewModel.MunicipalityID);
                return View(viewModel);
            }

            newSupplier.Email = viewModel.Email;
            newSupplier.PhoneNumber = viewModel.PhoneNumber;
            newSupplier.DepartmentID = viewModel.DepartmentID;
            newSupplier.MunicipalityID = viewModel.MunicipalityID;
            newSupplier.StreetAddress = viewModel.StreetAddress?.ToUpper();
            newSupplier.IsActive = true;

            _context.Add(newSupplier);
            await _context.SaveChangesAsync();
            TempData["success"] = "Proveedor creado exitosamente.";
            await _auditService.LogAsync("Proveedores", $"Creó el proveedor '{newSupplier.DisplayName}' (ID: {newSupplier.SupplierID}).");
            return RedirectToAction(nameof(Index));
        }

        // GET: Suppliers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();


            if (supplier is NaturalPersonSupplier naturalPerson)
            {
                var viewModel = new NaturalPersonSupplierEditViewModel
                {
                    SupplierID = naturalPerson.SupplierID,
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
                return View("EditNaturalPersonSupplier", viewModel);
            }

            if (supplier is LegalEntitySupplier legalEntity)
            {
                var nitParts = legalEntity.NIT?.Split('-');
                var viewModel = new LegalEntitySupplierEditViewModel
                {
                    SupplierID = legalEntity.SupplierID,
                    CompanyName = legalEntity.CompanyName,
                    NitNumber = nitParts?.Length > 0 ? nitParts[0] : legalEntity.NIT,
                    VerificationDigit = nitParts?.Length > 1 ? nitParts[1] : null,
                    Email = legalEntity.Email ?? string.Empty,
                    PhoneNumber = legalEntity.PhoneNumber ?? string.Empty,
                    DepartmentID = legalEntity.DepartmentID,
                    MunicipalityID = legalEntity.MunicipalityID,
                    StreetAddress = legalEntity.StreetAddress,
                    IsActive = legalEntity.IsActive
                };
                await PopulateDropdowns(legalEntity.DepartmentID, legalEntity.MunicipalityID);
                return View("EditLegalEntitySupplier", viewModel);
            }

            return NotFound();
        }

        // POST: Suppliers/EditNaturalPersonSupplier/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditNaturalPersonSupplier(int id, NaturalPersonSupplierEditViewModel viewModel)
        {
            if (id != viewModel.SupplierID) return NotFound();

            if (await _context.NaturalPersonSuppliers.AnyAsync(s => s.DocumentNumber == viewModel.DocumentNumber && s.SupplierID != id))
            {
                ModelState.AddModelError("DocumentNumber", "Este número de documento ya está registrado para otro proveedor.");
            }

            if (ModelState.IsValid)
            {
                var supplierToUpdate = await _context.NaturalPersonSuppliers.FindAsync(id);
                if (supplierToUpdate == null) return NotFound();

                supplierToUpdate.FirstName = viewModel.FirstName?.ToUpper() ?? string.Empty;
                supplierToUpdate.LastName = viewModel.LastName?.ToUpper() ?? string.Empty;
                supplierToUpdate.DocumentType = viewModel.DocumentType;
                supplierToUpdate.DocumentNumber = viewModel.DocumentNumber?.ToUpper() ?? string.Empty;
                supplierToUpdate.Email = viewModel.Email ?? string.Empty;
                supplierToUpdate.PhoneNumber = viewModel.PhoneNumber ?? string.Empty;
                supplierToUpdate.DepartmentID = viewModel.DepartmentID;
                supplierToUpdate.MunicipalityID = viewModel.MunicipalityID;
                supplierToUpdate.StreetAddress = viewModel.StreetAddress?.ToUpper();
                supplierToUpdate.IsActive = viewModel.IsActive;

                try
                {
                    _context.Update(supplierToUpdate);
                    await _context.SaveChangesAsync();
                    TempData["success"] = "Proveedor natural actualizado exitosamente.";
                    await _auditService.LogAsync("Proveedores", $"Editó el proveedor '{supplierToUpdate.DisplayName}' (ID: {supplierToUpdate.SupplierID}).");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SupplierExists(viewModel.SupplierID)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            await PopulateDropdowns(viewModel.DepartmentID, viewModel.MunicipalityID);
            return View("EditNaturalPersonSupplier", viewModel);
        }

        // POST: Suppliers/EditLegalEntitySupplier/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLegalEntitySupplier(int id, LegalEntitySupplierEditViewModel viewModel)
        {
            if (id != viewModel.SupplierID) return NotFound();

            if (!string.IsNullOrEmpty(viewModel.NitNumber) && !string.IsNullOrEmpty(viewModel.VerificationDigit))
            {
                var calculatedDv = ValidationHelper.CalculateNitVerificationDigit(viewModel.NitNumber);
                if (calculatedDv < 0 || calculatedDv.ToString() != viewModel.VerificationDigit)
                {
                    ModelState.AddModelError("VerificationDigit", "El dígito de verificación no es válido para el NIT proporcionado.");
                }
            }

            var fullNit = $"{viewModel.NitNumber}-{viewModel.VerificationDigit}";

            if (await _context.LegalEntitySuppliers.AnyAsync(s => s.NIT == fullNit && s.SupplierID != id))
            {
                ModelState.AddModelError("NitNumber", "Este NIT ya está registrado para otro proveedor.");
            }


            if (ModelState.IsValid)
            {
                try
                {
                    var supplierToUpdate = await _context.LegalEntitySuppliers.FindAsync(id);
                    if (supplierToUpdate == null) return NotFound();

                    supplierToUpdate.CompanyName = viewModel.CompanyName?.ToUpper() ?? string.Empty;
                    supplierToUpdate.NIT = fullNit.ToUpper();
                    supplierToUpdate.Email = viewModel.Email ?? string.Empty;
                    supplierToUpdate.PhoneNumber = viewModel.PhoneNumber ?? string.Empty;
                    supplierToUpdate.DepartmentID = viewModel.DepartmentID;
                    supplierToUpdate.MunicipalityID = viewModel.MunicipalityID;
                    supplierToUpdate.StreetAddress = viewModel.StreetAddress?.ToUpper();

                    supplierToUpdate.IsActive = viewModel.IsActive;

                    _context.Update(supplierToUpdate);
                    await _context.SaveChangesAsync();
                    TempData["success"] = "Proveedor jurídico actualizado exitosamente.";
                    await _auditService.LogAsync("Proveedores", $"Editó el proveedor '{supplierToUpdate.DisplayName}' (ID: {supplierToUpdate.SupplierID}).");
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SupplierExists(viewModel.SupplierID)) return NotFound();
                    else throw;
                }
            }

            await PopulateDropdowns(viewModel.DepartmentID, viewModel.MunicipalityID);
            return View("EditLegalEntitySupplier", viewModel);
        }


        // GET: Suppliers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var supplier = await _context.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.SupplierID == id);

            if (supplier == null) return NotFound();

            return View(supplier);
        }

        // POST: Suppliers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier != null)
            {
                var supplierName = supplier.DisplayName;
                var supplierId = supplier.SupplierID;
                supplier.IsActive = false;
                _context.Update(supplier);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Suppliers", $"Eliminó (desactivó) el proveedor '{supplierName}' (ID: {supplierId}).");
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: /Suppliers/GetMunicipalities/5
        [HttpGet]
        [AllowAnonymous]
        public async Task<JsonResult> GetMunicipalities(string departmentId)
        {
            var municipalities = await _context.Municipalities
                .Where(m => m.DepartmentID == departmentId)
                .OrderBy(m => m.Name)
                .Select(m => new { value = m.MunicipalityID, text = m.Name })
                .ToListAsync();
            return Json(municipalities);
        }

        private bool SupplierExists(int id)
        {
            return _context.Suppliers.Any(e => e.SupplierID == id);
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
