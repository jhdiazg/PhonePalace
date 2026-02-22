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
using Microsoft.Extensions.Configuration;

namespace PhonePalace.Web.Controllers
{
    [Route("CuentasPorCobrar")]
    public class AccountReceivablesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICashService _cashService;
        private readonly IAuditService _auditService;
        private readonly IBankService _bankService;
        private readonly IConfiguration _config;

        public AccountReceivablesController(ApplicationDbContext context, ICashService cashService, IAuditService auditService, IBankService bankService, IConfiguration config)
        {
            _context = context;
            _cashService = cashService;
            _auditService = auditService;
            _bankService = bankService;
            _config = config;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? clientName, string status = "Pending", string? type = null, DateTime? startDate = null, DateTime? endDate = null, int? pageNumber = null, int? pageSize = null)
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

            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(ar => ar.Type == type);
            }

            if (startDate.HasValue)
            {
                query = query.Where(ar => ar.Date >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                query = query.Where(ar => ar.Date < endDate.Value.Date.AddDays(1));
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
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            // Cargar lista de tipos para el filtro
            var types = await _context.AccountReceivables.Select(x => x.Type).Distinct().OrderBy(t => t).ToListAsync();
            ViewBag.TypeList = new SelectList(types, type);
            ViewBag.CurrentType = type;

            return View(list);
        }

        [HttpGet("Create")]
        public IActionResult Create()
        {
            ViewBag.Clients = new SelectList(_context.Clients.Where(c => c.IsActive), "ClientID", "DisplayName");
            ViewBag.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>()
                .Where(x => x.Value == PaymentMethod.Cash.ToString() || x.Value == PaymentMethod.Transfer.ToString());
            ViewBag.Banks = new SelectList(_context.Banks.Where(b => b.IsActive), "BankID", "Name");
            return View(new LoanCreateViewModel { Date = DateTime.Now, Description = string.Empty });
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LoanCreateViewModel model, PaymentMethod disbursementMethod, int? bankId)
        {
            if (ModelState.IsValid)
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync<IActionResult>(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        // Validar caja abierta SOLO si el desembolso es en Efectivo
                        if (disbursementMethod == PaymentMethod.Cash)
                        {
                            var currentCash = await _cashService.GetCurrentCashRegisterAsync();
                            if (currentCash == null)
                            {
                                throw new InvalidOperationException("Debe abrir la caja antes de registrar un préstamo en efectivo.");
                            }

                            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                            if (string.IsNullOrEmpty(userId)) return Unauthorized();

                            // 1. Registrar Egreso de Caja
                            await _cashService.RegisterExpenseAsync(model.Amount, $"Préstamo a cliente: {model.Description}", userId);
                        }
                        else if (disbursementMethod == PaymentMethod.Transfer)
                        {
                            // Desembolso por Banco (Egreso)
                            if (!bankId.HasValue)
                            {
                                throw new InvalidOperationException("Debe seleccionar un banco para el desembolso.");
                            }

                            var bank = await _context.Banks.FindAsync(bankId.Value);
                            if (bank != null)
                            {
                                var bankTransaction = new BankTransaction
                                {
                                    BankID = bank.BankID,
                                    Date = DateTime.Now,
                                    Amount = -model.Amount, // Egreso es negativo
                                    Description = $"Desembolso Préstamo: {model.Description}",
                                    BalanceAfterTransaction = bank.Balance - model.Amount
                                };
                                _context.BankTransactions.Add(bankTransaction);
                                bank.Balance -= model.Amount;
                                _context.Update(bank);
                            }
                        }

                        // 2. Obtener el cliente
                        var client = await _context.Clients.FindAsync(model.ClientID);
                        if (client == null)
                        {
                            throw new InvalidOperationException("Cliente no encontrado.");
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
                        await transaction.CommitAsync();

                        return RedirectToAction(nameof(Index));
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        ModelState.AddModelError("", ex.Message);
                        ViewBag.Clients = new SelectList(_context.Clients.Where(c => c.IsActive), "ClientID", "DisplayName", model.ClientID);
                        ViewBag.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>()
                            .Where(x => x.Value == PaymentMethod.Cash.ToString() || x.Value == PaymentMethod.Transfer.ToString());
                        ViewBag.Banks = new SelectList(_context.Banks.Where(b => b.IsActive), "BankID", "Name");
                        return View(model);
                    }
                });
            }

            ViewBag.Clients = new SelectList(_context.Clients.Where(c => c.IsActive), "ClientID", "DisplayName", model.ClientID);
            ViewBag.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>()
                .Where(x => x.Value == PaymentMethod.Cash.ToString() || x.Value == PaymentMethod.Transfer.ToString());
            ViewBag.Banks = new SelectList(_context.Banks.Where(b => b.IsActive), "BankID", "Name");
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

            ViewBag.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>()
                .Where(x => x.Value == PaymentMethod.Cash.ToString() || 
                            x.Value == PaymentMethod.Transfer.ToString() ||
                            x.Value == PaymentMethod.DebitCard.ToString() ||
                            x.Value == PaymentMethod.CreditCard.ToString());
            ViewBag.Banks = new SelectList(_context.Banks.Where(b => b.IsActive), "BankID", "Name");
            ViewBag.DefaultCardBankId = _config.GetValue<int?>("Defaults:CardBankID");

            return View(ar);
        }

        [HttpPost("AddPayment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPayment(int id, decimal amount, string note, PaymentMethod paymentMethod, int? bankId)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<IActionResult>(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
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

            // Preparar el objeto de pago primero para poder enlazarlo si es necesario
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

            // 2. Registrar Pago en Historial (Lo agregamos antes para asegurar que la entidad esté rastreada)
            _context.AccountReceivablePayments.Add(payment);

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
            else if (paymentMethod == PaymentMethod.CreditCard)
            {
                if (!bankId.HasValue)
                {
                    TempData["Error"] = "Debe seleccionar un banco para pagos con tarjeta.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Crear Verificación de Tarjeta de Crédito (Pendiente)
                var verification = new CreditCardVerification
                {
                    SaleID = ar.SaleID, // Si viene de una venta, enlazamos. Si es préstamo, será null.
                    BankID = bankId.Value,
                    Amount = amount,
                    CreationDate = DateTime.Now,
                    Status = VerificationStatus.Pending,
                    AccountReceivablePayment = payment // Enlazamos el pago de CxC
                };
                _context.CreditCardVerifications.Add(verification);
            }
            else if (paymentMethod == PaymentMethod.Transfer || paymentMethod == PaymentMethod.DebitCard)
            {
                if (!bankId.HasValue)
                {
                    TempData["Error"] = "Debe seleccionar un banco/cuenta destino.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var bank = await _context.Banks.FindAsync(bankId.Value);
                if (bank != null)
                {
                            var bankTransaction = new BankTransaction
                    {
                        BankID = bank.BankID,
                        Date = DateTime.Now,
                        Amount = amount, // Ingreso es positivo
                        Description = $"Abono CxC #{ar.AccountReceivableID} ({ar.Type}) - {EnumHelper.GetDisplayName(paymentMethod)}",
                        BalanceAfterTransaction = bank.Balance + amount
                    };
                            _context.BankTransactions.Add(bankTransaction);
                    bank.Balance += amount;
                    _context.Update(bank);
                }
            }

            // 3. Actualizar Saldo
            ar.Balance -= amount;
            if (ar.Balance <= 0.01m)
            {
                ar.Balance = 0;
                ar.IsPaid = true;
            }

            await _context.SaveChangesAsync();
            await _auditService.LogAsync("CuentasPorCobrar", $"Registró abono de {amount:C} a la cuenta #{id}. Nuevo saldo: {ar.Balance:C}");
            await transaction.CommitAsync();
            TempData["Success"] = "Abono registrado correctamente.";

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

        [HttpPost("Delete/{id?}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var ar = await _context.AccountReceivables
                .Include(x => x.Client)
                .FirstOrDefaultAsync(x => x.AccountReceivableID == id);
            if (ar == null) return NotFound();

            if (ar.Balance < ar.TotalAmount)
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
