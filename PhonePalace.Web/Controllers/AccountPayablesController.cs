using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using PhonePalace.Domain.Entities;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Web.Helpers;
using PhonePalace.Web.ViewModels;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Security.Claims;
using PhonePalace.Domain.Enums;

namespace PhonePalace.Web.Controllers
{
    [Authorize]
    public class AccountPayablesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly ICashService _cashService;
        private readonly IBankService _bankService;

        public AccountPayablesController(ApplicationDbContext context, IAuditService auditService, ICashService cashService, IBankService bankService)
        {
            _context = context;
            _auditService = auditService;
            _cashService = cashService;
            _bankService = bankService;
        }

        [Authorize(Roles = "Administrador,Cajero,Contador")]
        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, string status = "Pending", string? type = null, int? pageNumber = null, int? pageSize = null)
        {
            if (startDate.HasValue && endDate.HasValue && startDate.Value > endDate.Value)
            {
                TempData["Error"] = "La fecha de inicio no puede ser mayor que la fecha de fin.";
            }

            ViewData["PageSize"] = pageSize ?? 10;
            ViewData["StartDate"] = startDate?.ToString("yyyy-MM-dd");
            ViewData["EndDate"] = endDate?.ToString("yyyy-MM-dd");
            ViewData["CurrentStatus"] = status;
            ViewData["CurrentType"] = type;

            var accountPayablesQuery = _context.AccountPayables
                .AsNoTracking() // Improves performance for read-only queries
                .Include(a => a.Purchase!)
                .ThenInclude(p => p!.Supplier)
                .AsQueryable();

            if (User.IsInRole("Contador"))
            {
                // Para el contador, solo mostrar CxP de compras que tienen IVA.
                accountPayablesQuery = accountPayablesQuery.Where(a => a.PurchaseId.HasValue && _context.PurchaseDetails.Any(pd => pd.PurchaseId == a.PurchaseId && pd.TaxRate > 0));
            }

            if (startDate.HasValue)
            {
                accountPayablesQuery = accountPayablesQuery.Where(a => a.DueDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                accountPayablesQuery = accountPayablesQuery.Where(a => a.DueDate < endDate.Value.Date.AddDays(1));
            }

            if (!string.IsNullOrEmpty(type))
            {
                accountPayablesQuery = accountPayablesQuery.Where(a => a.Type == type);
            }

            switch (status)
            {
                case "Paid":
                    accountPayablesQuery = accountPayablesQuery.Where(a => a.IsPaid);
                    break;
                case "Pending":
                    accountPayablesQuery = accountPayablesQuery.Where(a => !a.IsPaid);
                    break;
            }

            ViewBag.StatusList = new List<SelectListItem>
            {
                new SelectListItem { Text = "Pendientes", Value = "Pending", Selected = status == "Pending" },
                new SelectListItem { Text = "Pagadas", Value = "Paid", Selected = status == "Paid" },
                new SelectListItem { Text = "Todas", Value = "All", Selected = status == "All" }
            };

            // Cargar lista de tipos existentes para el filtro desplegable
            var types = await _context.AccountPayables
                .Select(a => a.Type)
                .Distinct()
                .OrderBy(t => t)
               .ToListAsync();
            ViewBag.TypeList = new SelectList(types, type);

            // Calcular totales antes de paginar
            ViewBag.TotalPayable = await accountPayablesQuery.SumAsync(x => x.Balance);
            
            var viewModelQuery = accountPayablesQuery.Select(a => new AccountPayableIndexViewModel
            {
                Id = a.Id,
                DocumentType = EnumHelper.GetDisplayName(a.DocumentType),
                DocumentNumber = a.DocumentNumber,
                Beneficiary = a.Purchase != null ? a.Purchase.Supplier.DisplayName : a.Beneficiary,
                Amount = a.Amount,
                Balance = a.Balance,
                DueDate = a.DueDate,
                IsPaid = a.IsPaid,
                PurchaseId = a.PurchaseId,
                Type = a.Type
            });

            // Ordenamos por fecha de vencimiento para ver primero lo más urgente
            var paginatedResult = await PaginatedList<AccountPayableIndexViewModel>.CreateAsync(viewModelQuery.OrderBy(a => a.DueDate), pageNumber ?? 1, pageSize ?? 10);
            return View(paginatedResult);
        }

        [Authorize(Roles = "Administrador,Cajero,Contador")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var accountPayable = await _context.AccountPayables
                .Include(a => a.Purchase).ThenInclude(p => p.PurchaseDetails)
                .Include(a => a.Payments)
                    .ThenInclude(p => p.Bank)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (accountPayable == null) return NotFound();

            if (User.IsInRole("Contador"))
            {
                // Si es una CxP manual (sin compra), no se muestra al contador.
                // Si tiene compra, se valida que la compra tenga IVA.
                bool hasVat = accountPayable.Purchase != null && accountPayable.Purchase.PurchaseDetails.Any(d => d.TaxRate > 0);

                if (!hasVat)
                {
                    TempData["Error"] = "Los contadores solo pueden ver cuentas por pagar de compras con IVA.";
                    return RedirectToAction(nameof(Index));
                }
            }

            ViewBag.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>();
            ViewBag.Banks = new SelectList(await _context.Banks.Where(b => b.IsActive).ToListAsync(), "BankID", "Name");
            return View(accountPayable);
        }

        [Authorize(Roles = "Administrador,Cajero")]
        public async Task<IActionResult> Create()
        {
            // Cargamos los proveedores para que el usuario seleccione uno existente
            var suppliers = await _context.Suppliers.ToListAsync();
            ViewData["Beneficiary"] = new SelectList(suppliers, "DisplayName", "DisplayName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Cajero")]
        public async Task<IActionResult> Create([Bind("DocumentType,DocumentNumber,Amount,DueDate,Beneficiary,Type")] AccountPayable accountPayable)
        {
            // Regla de negocio: El estado inicial no puede ser Pagada
            accountPayable.IsPaid = false;
            accountPayable.CreatedDate = DateTime.Now;
            accountPayable.DocumentNumber = accountPayable.DocumentNumber?.ToUpper();
            accountPayable.Balance = accountPayable.Amount; // Inicializar saldo igual al monto

            if (ModelState.IsValid)
            {
                _context.Add(accountPayable);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Cuentas por Pagar", $"Creó la cuenta por pagar manual #{accountPayable.Id}.");
                return RedirectToAction(nameof(Index));
            }

            // Recargar la lista en caso de error de validación
            var suppliersList = await _context.Suppliers.ToListAsync();
            ViewData["Beneficiary"] = new SelectList(suppliersList, "DisplayName", "DisplayName", accountPayable.Beneficiary);
            return View(accountPayable);
        }

        [Authorize(Roles = "Administrador,Cajero")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var accountPayable = await _context.AccountPayables.FindAsync(id);
            if (accountPayable == null) return NotFound();
            return View(accountPayable);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Cajero")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,DocumentType,DocumentNumber,DueDate,IsPaid,Beneficiary")] AccountPayable formModel)
        {
            if (id != formModel.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var apToUpdate = await _context.AccountPayables.FindAsync(id);
                if (apToUpdate == null)
                {
                    return NotFound();
                }

                // This is a safer update pattern. We load the entity from the DB
                // and then update only the properties that should be changed from the form.
                apToUpdate.DocumentType = formModel.DocumentType;
                apToUpdate.DocumentNumber = formModel.DocumentNumber?.ToUpper();
                apToUpdate.DueDate = formModel.DueDate;
                apToUpdate.IsPaid = formModel.IsPaid;

                // Beneficiary should only be editable for manual accounts payable.
                if (apToUpdate.PurchaseId == null)
                {
                    apToUpdate.Beneficiary = formModel.Beneficiary;
                }

                try
                {
                    await _context.SaveChangesAsync();
                    await _auditService.LogAsync("Cuentas por Pagar", $"Editó la cuenta por pagar #{apToUpdate.Id}.");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AccountPayableExists(apToUpdate.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(formModel);
        }

        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var accountPayable = await _context.AccountPayables
                .Include(a => a.Purchase)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (accountPayable == null) return NotFound();

            return View(accountPayable);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var accountPayable = await _context.AccountPayables.FindAsync(id);
            if (accountPayable != null)
            {
                _context.AccountPayables.Remove(accountPayable);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Cuentas por Pagar", $"Eliminó la cuenta por pagar #{accountPayable.Id}.");
            }
            return RedirectToAction(nameof(Index));
        }

        private bool AccountPayableExists(int id)
        {
            return _context.AccountPayables.Any(e => e.Id == id);
        }

        [HttpGet("Pay/{id}")]
        [Authorize(Roles = "Administrador,Cajero")]
        public async Task<IActionResult> Pay(int? id)
        {
            if (id == null) return NotFound();

            var ap = await _context.AccountPayables.FindAsync(id);
            if (ap == null) return NotFound();

            if (ap.IsPaid)
            {
                TempData["Info"] = "Esta cuenta ya se encuentra pagada.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewBag.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>();
            ViewBag.Banks = new SelectList(await _context.Banks.Where(b => b.IsActive).ToListAsync(), "BankID", "Name");

            return View(ap);
        }

        [HttpPost("Pay/{id?}")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Cajero")]
        public async Task<IActionResult> Pay(int id, decimal amount, string note, PaymentMethod paymentMethod, int? bankId)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<IActionResult>(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var ap = await _context.AccountPayables.FindAsync(id);
                    if (ap == null) return NotFound();

                    if (ap.IsPaid)
                    {
                        TempData["Error"] = "Esta cuenta ya está pagada.";
                        return RedirectToAction(nameof(Details), new { id });
                    }

                    // Validar que el abono sea positivo y no exceda la deuda actual
                    if (amount <= 0 || amount > ap.Balance)
                    {
                        TempData["Error"] = $"El monto del abono no es válido. Saldo pendiente: {ap.Balance:C}";
                        return RedirectToAction(nameof(Details), new { id });
                    }

                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (string.IsNullOrEmpty(userId)) return Unauthorized();

                    // Validar banco si es necesario
                    if ((paymentMethod == PaymentMethod.Transfer || paymentMethod == PaymentMethod.DebitCard || paymentMethod == PaymentMethod.CreditCard) && !bankId.HasValue)
                    {
                        TempData["Error"] = "Debe seleccionar un banco para este método de pago.";
                        return RedirectToAction(nameof(Details), new { id });
                    }

                    // 1. Registrar Egreso (Caja o Banco)
                    if (paymentMethod == PaymentMethod.Cash)
                    {
                        var currentCash = await _cashService.GetCurrentCashRegisterAsync();
                        if (currentCash == null)
                        {
                            throw new InvalidOperationException("Debe abrir la caja para realizar pagos en efectivo.");
                        }
                        await _cashService.RegisterExpenseAsync(amount, $"Pago de CxP #{ap.Id} - {note}", userId);
                    }
                    else if (bankId.HasValue)
                    {
                        // Registrar movimiento bancario de salida (TransferOut)
                        await _bankService.RegisterManualMovementAsync(bankId.Value, BankTransactionType.TransferOut, amount, $"Pago de CxP #{ap.Id} ({EnumHelper.GetDisplayName(paymentMethod)}) - {note}");
                    }

                    // 2. Crear el registro del pago (abono)
                    var paymentRecord = new AccountPayablePayment
                    {
                        AccountPayableId = ap.Id,
                        Amount = amount,
                        PaymentDate = DateTime.Now,
                        PaymentMethod = paymentMethod,
                        BankId = bankId,
                        Note = note
                    };
                    _context.Set<AccountPayablePayment>().Add(paymentRecord);

                    // 3. Actualizar saldo de la cuenta por pagar
                    ap.Balance -= amount;
                    if (ap.Balance <= 0.01m)
                    {
                        ap.Balance = 0;
                        ap.IsPaid = true;
                        
                        // Actualizar el estado de la compra asociada a Pagada solo si se salda la cuenta
                        if (ap.PurchaseId.HasValue)
                        {
                            var purchase = await _context.Purchases.FindAsync(ap.PurchaseId.Value);
                            if (purchase != null)
                            {
                                purchase.Status = PurchaseStatus.Paid;
                                _context.Update(purchase);
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    await _auditService.LogAsync("CuentasPorPagar", $"Pagó la cuenta #{ap.Id} por {amount:C}.");
                    await transaction.CommitAsync();
                    TempData["Success"] = "Pago registrado correctamente.";

                    return RedirectToAction(nameof(Details), new { id });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] = ex.Message;
                    return RedirectToAction(nameof(Details), new { id });
                }
            });
        }

        [HttpGet]
        [Authorize(Roles = "Administrador")]
        [Route("CuentasPorPagar/ActualizarTiposAntiguos")]
        public async Task<IActionResult> UpdateOldPayableTypes()
        {
            // Busca todas las CxP que tienen un PurchaseId y cuyo tipo aún no ha sido establecido.
            var payablesToUpdate = await _context.AccountPayables
                .Where(ap => ap.PurchaseId != null && (ap.Type == null || ap.Type == ""))
                .ToListAsync();

            if (!payablesToUpdate.Any())
            {
                TempData["Info"] = "No se encontraron Cuentas por Pagar de compras que necesiten ser actualizadas.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var payable in payablesToUpdate)
            {
                payable.Type = "Proveedor";
            }

            int count = await _context.SaveChangesAsync();

            await _auditService.LogAsync("Sistema", $"Actualización masiva de tipos de CxP. Se actualizaron {count} registros a 'Proveedor'.");
            TempData["Success"] = $"Se han actualizado {count} Cuentas por Pagar antiguas al tipo 'Proveedor'.";

            return RedirectToAction(nameof(Index));
        }
    }
}