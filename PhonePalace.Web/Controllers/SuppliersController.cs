using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Enums;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.Helpers;
using PhonePalace.Web.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Administrador,Almacenista")]
    public class SuppliersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public SuppliersController(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        public async Task<IActionResult> Index(string searchString, int? pageNumber, int? pageSize)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["PageSize"] = pageSize ?? 10;

            var query = _context.Suppliers.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(s =>
                    (s is NaturalPersonSupplier && (
                        ((NaturalPersonSupplier)s).FirstName.Contains(searchString) ||
                        ((NaturalPersonSupplier)s).LastName.Contains(searchString) ||
                        ((NaturalPersonSupplier)s).DocumentNumber.Contains(searchString))) ||
                    (s is LegalEntitySupplier && (
                        (((LegalEntitySupplier)s).CompanyName != null && ((LegalEntitySupplier)s).CompanyName.Contains(searchString)) ||
                        (((LegalEntitySupplier)s).NIT != null && ((LegalEntitySupplier)s).NIT.Contains(searchString)))) ||
                    (s.Email != null && s.Email.Contains(searchString)));
            }

            var viewModelQuery = query
                .AsNoTracking()
                .Select(s => new SupplierIndexViewModel
                {
                    SupplierID = s.SupplierID,
                    DisplayName = s is NaturalPersonSupplier ? ((NaturalPersonSupplier)s).FirstName + " " + ((NaturalPersonSupplier)s).LastName : (s is LegalEntitySupplier ? (((LegalEntitySupplier)s).CompanyName ?? "") : ""),
                    Document = s is NaturalPersonSupplier ? ((NaturalPersonSupplier)s).DocumentNumber : (s is LegalEntitySupplier ? (((LegalEntitySupplier)s).NIT ?? "") : ""),
                    SupplierType = s is NaturalPersonSupplier ? "Persona Natural" : "Persona Jurídica",
                    Email = s.Email ?? "",
                    PhoneNumber = s.PhoneNumber ?? "",
                    IsActive = s.IsActive
                })
                .OrderBy(s => s.IsActive)
                .ThenBy(s => s.DisplayName);

            var paginated = await PaginatedList<SupplierIndexViewModel>.CreateAsync(viewModelQuery, pageNumber ?? 1, pageSize ?? 10);
            return View(paginated);
        }

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

        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View(new SupplierCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SupplierCreateViewModel viewModel)
        {
            if (viewModel.SupplierType == SupplierTypeSelection.LegalEntity)
            {
                if (!string.IsNullOrEmpty(viewModel.NitNumber) && !string.IsNullOrEmpty(viewModel.VerificationDigit))
                {
                    var calculatedDv = ValidationHelper.CalculateNitVerificationDigit(viewModel.NitNumber);
                    if (calculatedDv < 0 || calculatedDv.ToString() != viewModel.VerificationDigit)
                    {
                        ModelState.AddModelError("VerificationDigit", "El dígito de verificación no es válido.");
                    }
                }
            }

            if (ModelState.IsValid)
            {
                Supplier newSupplier;

                if (viewModel.SupplierType == SupplierTypeSelection.NaturalPerson)
                {
                    if (await _context.Suppliers.OfType<NaturalPersonSupplier>().AnyAsync(s => s.DocumentNumber == viewModel.DocumentNumber))
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
                else
                {
                    var fullNit = $"{viewModel.NitNumber}-{viewModel.VerificationDigit}";
                    if (await _context.Suppliers.OfType<LegalEntitySupplier>().AnyAsync(s => s.NIT == fullNit))
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

                newSupplier.Email = viewModel.Email;
                newSupplier.PhoneNumber = viewModel.PhoneNumber;
                newSupplier.DepartmentID = viewModel.DepartmentID;
                newSupplier.MunicipalityID = viewModel.MunicipalityID;
                newSupplier.StreetAddress = viewModel.StreetAddress?.ToUpper();
                newSupplier.IsActive = true;

                _context.Add(newSupplier);
                await _context.SaveChangesAsync();
                TempData["success"] = "Proveedor creado exitosamente.";
                string supplierName = viewModel.SupplierType == SupplierTypeSelection.NaturalPerson ? $"{viewModel.FirstName} {viewModel.LastName}" : viewModel.CompanyName!;
                await _auditService.LogAsync("Proveedores", $"Creó el proveedor '{supplierName.ToUpper()}' (ID: {newSupplier.SupplierID}).");
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdowns(viewModel.DepartmentID, viewModel.MunicipalityID);
            return View(viewModel);
        }

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
                return View("EditNaturalPerson", viewModel);
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditNaturalPerson(int id, NaturalPersonSupplierEditViewModel viewModel)
        {
            if (id != viewModel.SupplierID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var supplier = await _context.Suppliers.OfType<NaturalPersonSupplier>().FirstOrDefaultAsync(s => s.SupplierID == id);
                    if (supplier == null) return NotFound();

                    supplier.FirstName = viewModel.FirstName.ToUpper();
                    supplier.LastName = viewModel.LastName.ToUpper();
                    supplier.DocumentType = viewModel.DocumentType;
                    supplier.DocumentNumber = viewModel.DocumentNumber.ToUpper();
                    supplier.Email = viewModel.Email;
                    supplier.PhoneNumber = viewModel.PhoneNumber;
                    supplier.DepartmentID = viewModel.DepartmentID;
                    supplier.MunicipalityID = viewModel.MunicipalityID;
                    supplier.StreetAddress = viewModel.StreetAddress?.ToUpper();
                    supplier.IsActive = viewModel.IsActive;

                    _context.Update(supplier);
                    await _context.SaveChangesAsync();
                    TempData["success"] = "Proveedor actualizado exitosamente.";
                    await _auditService.LogAsync("Proveedores", $"Editó el proveedor '{supplier.FirstName} {supplier.LastName}' (ID: {supplier.SupplierID}).");
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SupplierExists(viewModel.SupplierID)) return NotFound();
                    else throw;
                }
            }
            await PopulateDropdowns(viewModel.DepartmentID, viewModel.MunicipalityID);
            return View("EditNaturalPerson", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLegalEntity(int id, LegalEntitySupplierEditViewModel viewModel)
        {
            if (id != viewModel.SupplierID) return NotFound();
            
            if (ModelState.IsValid)
            {
                var fullNit = $"{viewModel.NitNumber}-{viewModel.VerificationDigit}";
                try
                {
                    var supplier = await _context.Suppliers.OfType<LegalEntitySupplier>().FirstOrDefaultAsync(s => s.SupplierID == id);
                    if (supplier == null) return NotFound();

                    supplier.CompanyName = viewModel.CompanyName!.ToUpper();
                    supplier.NIT = fullNit.ToUpper();
                    supplier.Email = viewModel.Email;
                    supplier.PhoneNumber = viewModel.PhoneNumber;
                    supplier.DepartmentID = viewModel.DepartmentID;
                    supplier.MunicipalityID = viewModel.MunicipalityID;
                    supplier.StreetAddress = viewModel.StreetAddress?.ToUpper();
                    supplier.IsActive = viewModel.IsActive;

                    _context.Update(supplier);
                    await _context.SaveChangesAsync();
                    TempData["success"] = "Proveedor actualizado exitosamente.";
                    await _auditService.LogAsync("Proveedores", $"Editó el proveedor '{supplier.CompanyName}' (ID: {supplier.SupplierID}).");
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SupplierExists(viewModel.SupplierID)) return NotFound();
                    else throw;
                }
            }

            await PopulateDropdowns(viewModel.DepartmentID, viewModel.MunicipalityID);
            return View("EditLegalEntity", viewModel);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var supplier = await _context.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.SupplierID == id);

            if (supplier == null) return NotFound();

            return View(supplier);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier != null)
            {
                supplier.IsActive = false;
                _context.Update(supplier);
                await _context.SaveChangesAsync();
                string supplierName = supplier is NaturalPersonSupplier np ? $"{np.FirstName} {np.LastName}" : (supplier is LegalEntitySupplier le ? le.CompanyName : "Desconocido");
                await _auditService.LogAsync("Proveedores", $"Eliminó (desactivó) el proveedor '{supplierName}' (ID: {supplier.SupplierID}).");
            }
            return RedirectToAction(nameof(Index));
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

            // Se generan las listas para los enums usando el helper que sí lee los DisplayNames.
            ViewData["SupplierTypes"] = Enum.GetValues(typeof(SupplierTypeSelection))
                .Cast<SupplierTypeSelection>()
                .Select(e => new SelectListItem { 
                    Value = e.ToString(), 
                    Text = e.ToString() == "NaturalPerson" ? "Persona Natural" : (e.ToString() == "LegalEntity" ? "Persona Jurídica" : e.ToString())
                })
                .ToList();

            ViewData["DocumentTypes"] = Enum.GetValues(typeof(DocumentType))
                .Cast<Enum>()
                .Select(e => new SelectListItem { Value = e.ToString(), Text = EnumHelper.GetDisplayName(e) })
                .ToList();
        }
    }
}