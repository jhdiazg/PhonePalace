#nullable disable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Enums;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.ViewModels;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Administrador,Vendedor")]
    [Route("Caja")]
    public class CashController : Controller
    {
        private readonly ICashService _cashService;
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public CashController(ICashService cashService, ApplicationDbContext context, IAuditService auditService)
        {
            _cashService = cashService;
            _context = context;
            _auditService = auditService;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var currentCash = await _cashService.GetCurrentCashRegisterAsync();
            var model = new CashIndexViewModel();

            if (currentCash != null)
            {
                model.CurrentCashRegister = currentCash;

                // Obtener movimientos de la caja actual
                var movements = await _context.CashMovements
                    .Where(m => m.CashRegisterID == currentCash.CashRegisterID)
                    .OrderByDescending(m => m.MovementDate)
                    .ToListAsync();

                model.RecentMovements = movements.Take(10).ToList();

                // Calcular saldo actual: Apertura + Ingresos - Egresos
                decimal income = movements.Where(m => m.MovementType == CashMovementType.Income).Sum(m => m.Amount);
                decimal expense = movements.Where(m => m.MovementType == CashMovementType.Expense).Sum(m => m.Amount);
                
                model.CurrentBalance = currentCash.OpeningAmount + income - expense;
            }

            return View(model);
        }

        [HttpGet("Abrir")]
        public async Task<IActionResult> Open()
        {
            var currentCash = await _cashService.GetCurrentCashRegisterAsync();
            if (currentCash != null)
            {
                TempData["Info"] = "Ya hay una caja abierta.";
                return RedirectToAction("Index", "Home");
            }
            return View(new OpenCashViewModel());
        }

        [HttpPost("Abrir")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Open(OpenCashViewModel model, [FromForm] bool confirmDifference = false)
        {
            if (ModelState.IsValid)
            {
                var currentCash = await _cashService.GetCurrentCashRegisterAsync();
                if (currentCash != null)
                {
                    ModelState.AddModelError("", "Ya existe una caja abierta.");
                    return View(model);
                }

                // --- INICIO: Validación de diferencia con cierre anterior y confirmación ---
                var lastClosedCash = await _context.CashRegisters
                    .Where(cr => cr.ClosingDate.HasValue)
                    .OrderByDescending(cr => cr.ClosingDate)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                bool hasDifference = lastClosedCash != null && lastClosedCash.ClosingAmount != model.OpeningAmount;

                if (hasDifference && !confirmDifference)
                {
                    // Diferencia encontrada, se necesita confirmación.
                    ViewBag.ShowConfirmation = true;
                    ViewBag.LastClosingAmount = lastClosedCash.ClosingAmount;
                    ViewBag.OpeningAmount = model.OpeningAmount;
                    ViewBag.Difference = model.OpeningAmount - lastClosedCash.ClosingAmount;
                    return View(model);
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null) return Unauthorized();

                await _cashService.OpenCashRegisterAsync(model.OpeningAmount, userId);

                TempData["Success"] = "Caja abierta exitosamente.";
                if (hasDifference && lastClosedCash != null)
                {
                    decimal difference = model.OpeningAmount - (lastClosedCash.ClosingAmount ?? 0);
                    TempData["Warning"] = $"Caja abierta con una diferencia de {difference:C}.";
                    await _auditService.LogAsync("Caja", $"Apertura con diferencia. Cierre anterior: {lastClosedCash.ClosingAmount:C}, Apertura: {model.OpeningAmount:C}, Diferencia: {difference:C}.");
                }
                return RedirectToAction("Index", "Home");
            }
            return View(model);
        }

        [HttpGet("Cerrar")]
        public async Task<IActionResult> Close()
        {
            var currentCash = await _cashService.GetCurrentCashRegisterAsync();
            if (currentCash == null)
            {
                TempData["Info"] = "No hay una caja abierta para cerrar.";
                return RedirectToAction("Index", "Home");
            }

            var balance = await _cashService.GetCurrentBalanceAsync();

            var model = new CashCloseViewModel
            {
                SystemBalance = balance,
                ClosingAmount = balance // Sugerir el saldo del sistema por defecto
            };

            return View(model);
        }

        [HttpPost("Cerrar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(CashCloseViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null) return Unauthorized();

                try 
                {
                    await _cashService.CloseCashRegisterAsync(model.ClosingAmount, userId);
                    TempData["Success"] = "Caja cerrada exitosamente.";
                    return RedirectToAction("Index", "Home");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }
            return View(model);
        }

        [HttpGet("Ingreso")]
        public async Task<IActionResult> Income()
        {
            var currentCash = await _cashService.GetCurrentCashRegisterAsync();
            if (currentCash == null)
            {
                TempData["Info"] = "Debe abrir la caja para registrar ingresos.";
                return RedirectToAction("Index", "Home");
            }
            return View(new CashMovementViewModel());
        }

        [HttpPost("Ingreso")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Income(CashMovementViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null) return Unauthorized();

                await _cashService.RegisterIncomeAsync(model.Amount, model.Description, userId);
                TempData["Success"] = "Ingreso registrado exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpGet("Egreso")]
        public async Task<IActionResult> Expense()
        {
            var currentCash = await _cashService.GetCurrentCashRegisterAsync();
            if (currentCash == null)
            {
                TempData["Info"] = "Debe abrir la caja para registrar egresos.";
                return RedirectToAction("Index", "Home");
            }
            return View(new CashMovementViewModel());
        }

        [HttpPost("Egreso")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Expense(CashMovementViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null) return Unauthorized();

                var balance = await _cashService.GetCurrentBalanceAsync();
                if (balance < model.Amount)
                {
                    ModelState.AddModelError("Amount", $"Saldo insuficiente en caja. Disponible: {balance:C}");
                    return View(model);
                }

                await _cashService.RegisterExpenseAsync(model.Amount, model.Description, userId);
                TempData["Success"] = "Egreso registrado exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }
    }
}