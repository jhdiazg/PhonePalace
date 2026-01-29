using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using PhonePalace.Web.Helpers;
using PhonePalace.Web.ViewModels;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Administrador, Cajero")]
    public class CashController : Controller
    {
        private readonly ICashService _cashService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public CashController(ICashService cashService, UserManager<ApplicationUser> userManager, IAuditService auditService)
        {
            _cashService = cashService;
            _userManager = userManager;
            _auditService = auditService;
        }

        public async Task<IActionResult> Index()
        {
            var cashRegister = await _cashService.GetCurrentCashRegisterAsync();
            var balance = await _cashService.GetCurrentBalanceAsync();

            var viewModel = new CashIndexViewModel
            {
                CurrentCashRegister = cashRegister,
                CurrentBalance = balance,
                RecentMovements = (cashRegister?.CashMovements ?? new List<CashMovement>()).OrderByDescending(m => m.MovementDate).ToList()
            };

            return View(viewModel);
        }

        [HttpGet]
        public IActionResult Open()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Open(CashOpenViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("Usuario no autenticado.");
            }

            try
            {
                await _cashService.OpenCashRegisterAsync(model.OpeningBalance, userId);
                TempData["Success"] = "Caja abierta exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Close()
        {
            var currentBalance = await _cashService.GetCurrentBalanceAsync();
            return View(new CashCloseViewModel { SystemBalance = currentBalance });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(CashCloseViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.SystemBalance = await _cashService.GetCurrentBalanceAsync();
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("Usuario no autenticado.");
            }

            try
            {
                // Recalcular diferencia para el log de auditoría
                var systemBalance = await _cashService.GetCurrentBalanceAsync();
                var diff = model.ClosingAmount - systemBalance;
                string diffText = diff == 0 ? "Cuadre Perfecto" : (diff > 0 ? $"SOBRANTE {diff:C}" : $"FALTANTE {diff:C}");

                await _cashService.CloseCashRegisterAsync(model.ClosingAmount, userId);

                string auditMessage = $"Cierre de caja con monto final: {model.ClosingAmount:C} - {diffText}";

                await _auditService.LogAsync("Caja", auditMessage);

                TempData["Success"] = "Caja cerrada exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Income()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Income(CashMovementViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("Usuario no autenticado.");
            }

            try
            {
                await _cashService.RegisterIncomeAsync(model.Amount, (model.Description ?? string.Empty).ToUpper(), userId);
                TempData["Success"] = "Ingreso registrado exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Expense()
        {
            ViewBag.ExpenseTypes = EnumHelper.ToSelectList<ExpenseType>();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Expense(CashMovementViewModel model, ExpenseType expenseType)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ExpenseTypes = EnumHelper.ToSelectList<ExpenseType>();
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("Usuario no autenticado.");
            }

            try
            {
                var typeName = EnumHelper.GetDisplayName(expenseType);
                var fullDescription = $"{typeName}: {model.Description ?? string.Empty}".Trim();
                await _cashService.RegisterExpenseAsync(model.Amount, fullDescription.ToUpper(), userId);
                TempData["Success"] = "Egreso registrado exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
                ViewBag.ExpenseTypes = EnumHelper.ToSelectList<ExpenseType>();
                return View(model);
            }
        }
    }
}