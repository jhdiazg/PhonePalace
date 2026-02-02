using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public FixedExpensesController(ApplicationDbContext context, IAuditService auditService, ICashService cashService)
        {
            _context = context;
            _auditService = auditService;
            _cashService = cashService;
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

            // 3. Combinar
            var model = definitions.Select(d => new FixedExpenseStatusViewModel { FixedExpense = d, LastPayment = lastPayments.FirstOrDefault(p => p.FixedExpenseId == d.Id) }).ToList();

            return View(model);
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
            return View(viewModel);
        }

        // POST: FixedExpenses/Pay/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int id, FixedExpensePayViewModel model)
        {
            if (id != model.Id) return NotFound();

            var fixedExpense = await _context.FixedExpenses.FindAsync(id);
            if (fixedExpense == null) return NotFound();

            // Verificar si ya existe un pago para ese periodo
            var existingPayment = await _context.FixedExpensePayments
                .AnyAsync(p => p.FixedExpenseId == id && p.Period.Year == model.Year && p.Period.Month == model.Month);

            if (existingPayment)
            {
                ModelState.AddModelError("", $"Ya existe un pago registrado para el periodo {model.Month}/{model.Year}.");
                ViewBag.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>();
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

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
                        ModelState.AddModelError("", "La caja debe estar abierta para realizar pagos en efectivo.");
                        ViewBag.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>();
                        return View(model);
                    }

                    var cashMovement = await _cashService.RegisterExpenseAsync(model.Amount, $"PAGO GASTO FIJO: {fixedExpense.Concept.ToUpper()}", userId);
                    payment.CashMovementId = cashMovement.CashMovementID;
                }

                _context.FixedExpensePayments.Add(payment);
                await _context.SaveChangesAsync();
                
                await _auditService.LogAsync("Gastos Fijos", $"Registró pago de '{fixedExpense.Concept}' por {model.Amount:C} para el periodo {model.Month}/{model.Year}.");
                TempData["Success"] = "Pago registrado exitosamente.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Ocurrió un error: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }

            return RedirectToAction(nameof(Index));
        }
    }
}