using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Web.ViewModels;
using System.Security.Claims;

namespace PhonePalace.Web.Controllers
{
    [Route("CuentasPorCobrar")]
    public class AccountReceivablesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICashService _cashService;

        public AccountReceivablesController(ApplicationDbContext context, ICashService cashService)
        {
            _context = context;
            _cashService = cashService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? clientName, bool showPaid = false)
        {
            var query = _context.AccountReceivables
                .Include(ar => ar.Client)
                .AsQueryable();

            if (!showPaid)
            {
                query = query.Where(ar => !ar.IsPaid);
            }

            if (!string.IsNullOrEmpty(clientName))
            {
                query = query.Where(ar => ar.Client.DisplayName.Contains(clientName));
            }

            var list = await query.OrderByDescending(ar => ar.Date).ToListAsync();
            ViewBag.ShowPaid = showPaid;
            ViewBag.ClientName = clientName;
            
            // Calcular totales
            ViewBag.TotalReceivable = list.Where(x => !x.IsPaid).Sum(x => x.Balance);

            return View(list);
        }

        [HttpGet("Create")]
        public IActionResult Create()
        {
            ViewBag.Clients = new SelectList(_context.Clients.Where(c => c.IsActive), "ClientID", "DisplayName");
            return View(new LoanCreateViewModel { Date = DateTime.Now, Description = string.Empty });
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LoanCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Validar caja abierta para el egreso
                var currentCash = await _cashService.GetCurrentCashRegisterAsync();
                if (currentCash == null)
                {
                    ModelState.AddModelError("", "Debe abrir la caja antes de registrar un préstamo (salida de dinero).");
                }
                else
                {
                    string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (string.IsNullOrEmpty(userId))
                    {
                        return Unauthorized();
                    }

                    // 1. Registrar Egreso de Caja
                    // Asumimos que ICashService tiene un método para registrar gastos/egresos.
                    // Si no existe en la interfaz, deberás agregarlo: Task RegisterExpenseAsync(decimal amount, string description, string userId);
                    await _cashService.RegisterExpenseAsync(model.Amount, $"Préstamo a cliente: {model.Description}", userId);

                    // 2. Obtener el cliente
                    var client = await _context.Clients.FindAsync(model.ClientID);
                    if (client == null)
                    {
                        ModelState.AddModelError("", "Cliente no encontrado.");
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
                        Description = model.Description,
                        IsPaid = false
                    };

                    _context.AccountReceivables.Add(ar);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }
            }

            ViewBag.Clients = new SelectList(_context.Clients.Where(c => c.IsActive), "ClientID", "DisplayName", model.ClientID);
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

            return View(ar);
        }

        [HttpPost("AddPayment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPayment(int id, decimal amount, string note)
        {
            var ar = await _context.AccountReceivables.FindAsync(id);
            if (ar == null) return NotFound();

            if (amount <= 0 || amount > ar.Balance)
            {
                TempData["Error"] = "El monto del abono no es válido.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Validar caja
            var currentCash = await _cashService.GetCurrentCashRegisterAsync();
            if (currentCash == null)
            {
                TempData["Error"] = "Debe abrir la caja para recibir abonos.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 1. Registrar Ingreso en Caja
            await _cashService.RegisterIncomeAsync(amount, $"Abono a CxC #{ar.AccountReceivableID} ({ar.Type})", userId, null);

            // 2. Registrar Pago en Historial
            var payment = new AccountReceivablePayment
            {
                AccountReceivableID = ar.AccountReceivableID,
                AccountReceivable = ar,
                Date = DateTime.Now,
                Amount = amount,
                Note = note
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
            TempData["Success"] = "Abono registrado correctamente.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var ar = await _context.AccountReceivables.Include(x => x.Payments).FirstOrDefaultAsync(x => x.AccountReceivableID == id);
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
            TempData["Success"] = "Registro eliminado.";

            return RedirectToAction(nameof(Index));
        }
    }
}
