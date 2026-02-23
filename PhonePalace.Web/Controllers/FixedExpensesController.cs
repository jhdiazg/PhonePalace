using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.Helpers;
using PhonePalace.Web.ViewModels;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Globalization;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Administrador,Cajero")]
    public class FixedExpensesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly ICashService _cashService;
        private readonly IBankService _bankService;

        public FixedExpensesController(ApplicationDbContext context, IAuditService auditService, ICashService cashService, IBankService bankService)
        {
            _context = context;
            _auditService = auditService;
            _cashService = cashService;
            _bankService = bankService;
        }

        // GET: FixedExpenses
        public async Task<IActionResult> Index()
        {
            // 1. Obtener definiciones activas
            var definitions = await _context.FixedExpenses.Where(e => e.IsActive).OrderBy(e => e.Concept).ToListAsync();

            // 2. Obtener el ÚLTIMO pago realizado para cada gasto (para mostrar en la lista)
            var lastPayments = await _context.FixedExpensePayments
                .GroupBy(p => p.FixedExpenseId)
                .Select(g => g.OrderByDescending(p => p.Period).FirstOrDefault())
                .ToListAsync();

            // 3. Calcular el total pagado en el mes actual para cada gasto
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;
            var currentMonthTotals = await _context.FixedExpensePayments
                .Where(p => p.Period.Month == currentMonth && p.Period.Year == currentYear)
                .GroupBy(p => p.FixedExpenseId)
                .Select(g => new { Id = g.Key, Total = g.Sum(p => p.Amount) })
                .ToDictionaryAsync(x => x.Id, x => x.Total);

            ViewBag.CurrentMonthTotals = currentMonthTotals;

            // 4. Combinar
            var model = definitions.Select(d => new FixedExpenseStatusViewModel { FixedExpense = d, LastPayment = lastPayments.FirstOrDefault(p => p?.FixedExpenseId == d.Id) }).ToList();

            return View(model);
        }

        // GET: FixedExpenses/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var fixedExpense = await _context.FixedExpenses
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (fixedExpense == null) return NotFound();

            var payments = await _context.FixedExpensePayments
                .Where(p => p.FixedExpenseId == id)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            ViewBag.Payments = payments;

            return View(fixedExpense);
        }

        // GET: FixedExpenses/Create
        public IActionResult Create()
        {
            return View(new FixedExpense());
        }

        // POST: FixedExpenses/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Concept,Amount")] FixedExpense fixedExpense)
        {
            if (ModelState.IsValid)
            {
                fixedExpense.Concept = fixedExpense.Concept.ToUpper();
                fixedExpense.IsActive = true;

                _context.Add(fixedExpense);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Gastos Fijos", $"Creó la definición de gasto '{fixedExpense.Concept}'.");
                TempData["Success"] = "Gasto fijo creado exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            return View(fixedExpense);
        }

        // GET: FixedExpenses/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var fixedExpense = await _context.FixedExpenses.FindAsync(id);
            if (fixedExpense == null || !fixedExpense.IsActive) return NotFound();
            return View(fixedExpense);
        }

        // POST: FixedExpenses/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Concept,Amount")] FixedExpense fixedExpense)
        {
            if (id != fixedExpense.Id) return NotFound();

            var expenseToUpdate = await _context.FixedExpenses.FirstOrDefaultAsync(e => e.Id == id);
            if (expenseToUpdate == null || !expenseToUpdate.IsActive)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    expenseToUpdate.Concept = fixedExpense.Concept.ToUpper();
                    expenseToUpdate.Amount = fixedExpense.Amount;

                    await _context.SaveChangesAsync();
                    await _auditService.LogAsync("Gastos Fijos", $"Editó el gasto fijo '{expenseToUpdate.Concept}' (ID: {id}).");
                    TempData["Success"] = "Gasto fijo actualizado exitosamente.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.FixedExpenses.Any(e => e.Id == fixedExpense.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(fixedExpense);
        }

        // GET: FixedExpenses/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var fixedExpense = await _context.FixedExpenses.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
            if (fixedExpense == null) return NotFound();
            return View(fixedExpense);
        }

        // POST: FixedExpenses/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var fixedExpense = await _context.FixedExpenses.FindAsync(id);
            if (fixedExpense != null)
            {
                fixedExpense.IsActive = false;
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Gastos Fijos", $"Eliminó (lógicamente) el gasto fijo '{fixedExpense.Concept}' (ID: {id}).");
                TempData["Success"] = "Gasto fijo eliminado exitosamente.";
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: FixedExpenses/Pay/5
        public async Task<IActionResult> Pay(int? id)
        {
            if (id == null) return NotFound();
            var fixedExpense = await _context.FixedExpenses.FindAsync(id);
            if (fixedExpense == null || !fixedExpense.IsActive) return NotFound();

            var viewModel = new FixedExpensePayViewModel
            {
                Id = fixedExpense.Id,
                Concept = fixedExpense.Concept,
                Amount = fixedExpense.Amount, // Sugerir el monto original
                Year = DateTime.Now.Year,
                Month = DateTime.Now.Month,
                PaymentDate = DateTime.Now,
                PaymentMethod = PaymentMethod.Cash // Por defecto Efectivo
            };

            ViewBag.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>();
            ViewBag.Banks = new SelectList(await _context.Banks.Where(b => b.IsActive).ToListAsync(), "BankID", "Name");
            return View(viewModel);
        }

        // POST: FixedExpenses/Pay/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int id, FixedExpensePayViewModel model, int? bankId)
        {
            if (id != model.Id) return NotFound();

            var fixedExpense = await _context.FixedExpenses.FindAsync(id);
            if (fixedExpense == null) return NotFound();

            // Validar banco si el método de pago lo requiere
            if (model.PaymentMethod == PaymentMethod.Transfer && !bankId.HasValue)
            {
                ModelState.AddModelError("", "Debe seleccionar un banco para este método de pago.");
            }

            // Validar que no se exceda el monto del gasto fijo
            var totalPaid = await _context.FixedExpensePayments
                .Where(p => p.FixedExpenseId == id && p.Period.Year == model.Year && p.Period.Month == model.Month)
                .SumAsync(p => p.Amount);

            if (totalPaid + model.Amount > fixedExpense.Amount)
            {
                ModelState.AddModelError("", $"El pago excede el monto del gasto. Pagado: {totalPaid:C}, Nuevo: {model.Amount:C}, Límite: {fixedExpense.Amount:C}");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>();
                ViewBag.Banks = new SelectList(await _context.Banks.Where(b => b.IsActive).ToListAsync(), "BankID", "Name", bankId);
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                return await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var payment = new FixedExpensePayment
                        {
                            FixedExpenseId = id,
                            Period = new DateTime(model.Year, model.Month, 1),
                            PaymentDate = model.PaymentDate,
                            Amount = model.Amount,
                            PaymentMethod = model.PaymentMethod,
                            Notes = model.Notes
                        };

                        // Solo afectar caja si es Efectivo
                        if (model.PaymentMethod == PaymentMethod.Cash)
                        {
                            // Validar si la caja está abierta (opcional, pero recomendado)
                            var currentCash = await _cashService.GetCurrentCashRegisterAsync();
                            if (currentCash == null)
                            {
                                throw new InvalidOperationException("La caja debe estar abierta para realizar pagos en efectivo.");
                            }

                            var cashMovement = await _cashService.RegisterExpenseAsync(model.Amount, $"PAGO GASTO FIJO: {fixedExpense.Concept.ToUpper()}", userId);
                            payment.CashMovementId = cashMovement.CashMovementID;
                        }
                        else if (bankId.HasValue)
                        {
                            // Registrar egreso bancario
                            await _bankService.RegisterManualMovementAsync(bankId.Value, BankTransactionType.ManualExpense, model.Amount, $"PAGO GASTO FIJO: {fixedExpense.Concept.ToUpper()}");
                        }

                        _context.FixedExpensePayments.Add(payment);
                        await _context.SaveChangesAsync();
                        
                        await _auditService.LogAsync("Gastos Fijos", $"Registró pago de '{fixedExpense.Concept}' por {model.Amount:C} para el periodo {model.Month}/{model.Year}.");
                        await transaction.CommitAsync();
                        TempData["Success"] = "Pago registrado exitosamente.";
                        return RedirectToAction(nameof(Index));
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Ocurrió un error: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}