using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using PhonePalace.Domain.Entities;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Web.Helpers;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Security.Claims;
using PhonePalace.Domain.Enums;

namespace PhonePalace.Web.Controllers
{
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

        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, string status = "Pending", int? pageNumber = null, int? pageSize = null)
        {
            if (startDate.HasValue && endDate.HasValue && startDate.Value > endDate.Value)
            {
                TempData["Error"] = "La fecha de inicio no puede ser mayor a la fecha de fin.";
            }

            ViewData["PageSize"] = pageSize ?? 10;
            ViewData["StartDate"] = startDate?.ToString("yyyy-MM-dd");
            ViewData["EndDate"] = endDate?.ToString("yyyy-MM-dd");
            ViewData["CurrentStatus"] = status;

            var accountPayablesQuery = _context.AccountPayables
                .AsNoTracking() // Improves performance for read-only queries
                .Include(a => a.Purchase!)
                .ThenInclude(p => p!.Supplier)
                .AsQueryable();

            if (startDate.HasValue)
            {
                accountPayablesQuery = accountPayablesQuery.Where(a => a.DueDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                accountPayablesQuery = accountPayablesQuery.Where(a => a.DueDate < endDate.Value.Date.AddDays(1));
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

            // Calcular totales antes de paginar
            ViewBag.TotalPayable = await accountPayablesQuery.SumAsync(x => x.Balance);
            
            // Ordenamos por fecha de vencimiento para ver primero lo más urgente
            var accountPayables = await PaginatedList<AccountPayable>.CreateAsync(accountPayablesQuery.OrderBy(a => a.DueDate), pageNumber ?? 1, pageSize ?? 10);
            return View(accountPayables);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var accountPayable = await _context.AccountPayables
                .Include(a => a.Purchase)
                .Include(a => a.Payments)
                    .ThenInclude(p => p.Bank)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (accountPayable == null) return NotFound();

            ViewBag.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>();
            ViewBag.Banks = new SelectList(await _context.Banks.Where(b => b.IsActive).ToListAsync(), "BankID", "Name");
            return View(accountPayable);
        }

        public async Task<IActionResult> Create()
        {
            // Cargamos los proveedores para que el usuario seleccione uno existente
            var suppliers = await _context.Suppliers.ToListAsync();
            ViewData["Beneficiary"] = new SelectList(suppliers, "DisplayName", "DisplayName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("DocumentType,DocumentNumber,Amount,DueDate,Beneficiary")] AccountPayable accountPayable)
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

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var accountPayable = await _context.AccountPayables.FindAsync(id);
            if (accountPayable == null) return NotFound();
            return View(accountPayable);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,PurchaseId,DocumentType,DocumentNumber,CreatedDate,DueDate,IsPaid")] AccountPayable accountPayable)
        {
            if (id != accountPayable.Id) return NotFound();

            // Eliminamos Amount del Bind para no sobrescribirlo con 0 si está disabled en la vista
            // Recuperamos la entidad original para mantener el saldo y otros datos críticos
            var originalAp = await _context.AccountPayables.AsNoTracking().FirstOrDefaultAsync(ap => ap.Id == id);
            if (originalAp == null) return NotFound();

            // Mantenemos el monto original (saldo actual)
            accountPayable.Amount = originalAp.Amount;
            accountPayable.Balance = originalAp.Balance;

            if (ModelState.IsValid)
            {
                try
                {
                    accountPayable.DocumentNumber = accountPayable.DocumentNumber?.ToUpper();
                    _context.Update(accountPayable);
                    await _context.SaveChangesAsync();
                    await _auditService.LogAsync("Cuentas por Pagar", $"Editó la cuenta por pagar #{accountPayable.Id}.");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AccountPayableExists(accountPayable.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(accountPayable);
        }

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
    }
}