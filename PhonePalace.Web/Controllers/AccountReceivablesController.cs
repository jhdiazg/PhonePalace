using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Web.ViewModels;
using PhonePalace.Web.Helpers;
using System.Security.Claims;
using PhonePalace.Domain.Enums;

namespace PhonePalace.Web.Controllers
{
    [Route("CuentasPorCobrar")]
    public class AccountReceivablesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICashService _cashService;
        private readonly IAuditService _auditService;

        public AccountReceivablesController(ApplicationDbContext context, ICashService cashService, IAuditService auditService)
        {
            _context = context;
            _cashService = cashService;
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? clientName, string status = "Pending", int? pageNumber = null, int? pageSize = null)
        {
            ViewData["PageSize"] = pageSize ?? 10;

            var query = _context.AccountReceivables
                .Include(ar => ar.Client)
                .AsQueryable();

            switch (status)
            {
                case "Paid":
                    query = query.Where(ar => ar.IsPaid);
                    break;
                case "All":
                    break;
                case "Pending":
                default:
                    query = query.Where(ar => !ar.IsPaid);
                    break;
            }

            if (!string.IsNullOrEmpty(clientName))
            {
                query = query.Where(ar => 
                    (ar.Client is NaturalPerson && (
                        ((NaturalPerson)ar.Client).FirstName.Contains(clientName) || 
                        ((NaturalPerson)ar.Client).LastName.Contains(clientName) ||
                        (((NaturalPerson)ar.Client).FirstName + " " + ((NaturalPerson)ar.Client).LastName).Contains(clientName))) ||
                    (ar.Client is LegalEntity && ((LegalEntity)ar.Client).CompanyName.Contains(clientName)));
            }

            // Calcular totales antes de paginar
            ViewBag.TotalReceivable = await query.SumAsync(x => x.Balance);

            var list = await PaginatedList<AccountReceivable>.CreateAsync(query.OrderByDescending(ar => ar.Date).AsNoTracking(), pageNumber ?? 1, pageSize ?? 10);
            ViewBag.StatusList = new List<SelectListItem>
            {
                new SelectListItem { Text = "Pendientes", Value = "Pending", Selected = status == "Pending" },
                new SelectListItem { Text = "Pagadas", Value = "Paid", Selected = status == "Paid" },
                new SelectListItem { Text = "Todas", Value = "All", Selected = status == "All" }
            };
            ViewBag.CurrentStatus = status;
            ViewBag.ClientName = clientName;

            return View(list);
        }

        [HttpGet("Create")]
        public IActionResult Create()
        {
            ViewBag.Clients = new SelectList(_context.Clients.Where(c => c.IsActive), "ClientID", "DisplayName");
            ViewBag.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>();
            return View(new LoanCreateViewModel { Date = DateTime.Now, Description = string.Empty });
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LoanCreateViewModel model, PaymentMethod disbursementMethod)
        {
            if (ModelState.IsValid)
            {
                // Validar caja abierta SOLO si el desembolso es en Efectivo
                if (disbursementMethod == PaymentMethod.Cash)
                {
                    var currentCash = await _cashService.GetCurrentCashRegisterAsync();
                    if (currentCash == null)
                    {
                        ModelState.AddModelError("", "Debe abrir la caja antes de registrar un préstamo en efectivo.");
                        ViewBag.Clients = new SelectList(_context.Clients.Where(c => c.IsActive), "ClientID", "DisplayName", model.ClientID);
                        ViewBag.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>();
                        return View(model);
                    }

                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (string.IsNullOrEmpty(userId)) return Unauthorized();

                    // 1. Registrar Egreso de Caja
                    await _cashService.RegisterExpenseAsync(model.Amount, $"Préstamo a cliente: {model.Description}", userId);
                }

                // 2. Obtener el cliente
                var client = await _context.Clients.FindAsync(model.ClientID);
                if (client == null)
                {
                    ModelState.AddModelError("", "Cliente no encontrado.");
                    ViewBag.Clients = new SelectList(_context.Clients.Where(c => c.IsActive), "ClientID", "DisplayName", model.ClientID);
                    ViewBag.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>();
                    return View(model);
                }

                // 3. Crear Cuenta por Cobrar
                var ar = new AccountReceivable
                {
                    ClientID = model.ClientID,
                    Client = client,
                    Date = model.Date,
                    TotalAmount = model.Amount,
                    Balance = model.Amount,
                    Type = "Prestamo",
                    Description = $"{model.Description?.ToUpper()} ({EnumHelper.GetDisplayName(disbursementMethod)})",
                    IsPaid = false
                };

                _context.AccountReceivables.Add(ar);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Clients = new SelectList(_context.Clients.Where(c => c.IsActive), "ClientID", "DisplayName", model.ClientID);
            ViewBag.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>();
            return View(model);
        }

        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var ar = await _context.AccountReceivables
                .Include(x => x.Client)
                .Include(x => x.Payments)
                .FirstOrDefaultAsync(x => x.AccountReceivableID == id);

            if (ar == null) return NotFound();

            ViewBag.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>();

            return View(ar);
        }

        [HttpPost("AddPayment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPayment(int id, decimal amount, string note, PaymentMethod paymentMethod)
        {
            var ar = await _context.AccountReceivables.FindAsync(id);
            if (ar == null) return NotFound();

            if (amount <= 0 || amount > ar.Balance)
            {
                TempData["Error"] = "El monto del abono no es válido.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // 1. Validar caja y Registrar Ingreso solo si es Efectivo
            if (paymentMethod == PaymentMethod.Cash)
            {
                var currentCash = await _cashService.GetCurrentCashRegisterAsync();
                if (currentCash == null)
                {
                    TempData["Error"] = "Debe abrir la caja para recibir abonos en efectivo.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                await _cashService.RegisterIncomeAsync(amount, $"Abono a CxC #{ar.AccountReceivableID} ({ar.Type})", userId, null);
            }

            // 2. Registrar Pago en Historial
            string methodDesc = EnumHelper.GetDisplayName(paymentMethod);
            string finalNote = string.IsNullOrEmpty(note) ? $"[{methodDesc}]" : $"[{methodDesc}] {note.ToUpper()}";

            var payment = new AccountReceivablePayment
            {
                AccountReceivableID = ar.AccountReceivableID,
                AccountReceivable = ar,
                Date = DateTime.Now,
                Amount = amount,
                Note = finalNote
            };
            _context.AccountReceivablePayments.Add(payment);

            // 3. Actualizar Saldo
            ar.Balance -= amount;
            if (ar.Balance <= 0.01m)
            {
                ar.Balance = 0;
                ar.IsPaid = true;
            }

            await _context.SaveChangesAsync();
            await _auditService.LogAsync("CuentasPorCobrar", $"Registró abono de {amount:C} a la cuenta #{id}. Nuevo saldo: {ar.Balance:C}");
            TempData["Success"] = "Abono registrado correctamente.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var ar = await _context.AccountReceivables
                .Include(x => x.Payments)
                .Include(x => x.Client)
                .FirstOrDefaultAsync(x => x.AccountReceivableID == id);
            if (ar == null) return NotFound();

            if (ar.Payments.Any() || ar.Balance < ar.TotalAmount)
            {
                TempData["Error"] = "No se puede eliminar una cuenta que ya tiene abonos.";
                return RedirectToAction(nameof(Index));
            }

            // Si es préstamo, idealmente deberíamos revertir el egreso de caja, 
            // pero por simplicidad solo borramos el registro si fue un error de digitación inmediato.
            
            _context.AccountReceivables.Remove(ar);
            await _context.SaveChangesAsync();
            await _auditService.LogAsync("CuentasPorCobrar", $"Eliminó la cuenta por cobrar #{id} del cliente {ar.Client.DisplayName}.");
            TempData["Success"] = "Registro eliminado.";

            return RedirectToAction(nameof(Index));
        }
    }
}
