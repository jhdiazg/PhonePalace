using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Domain.Entities;
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

        public CashController(ICashService cashService, UserManager<ApplicationUser> userManager)
        {
            _cashService = cashService;
            _userManager = userManager;
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
                await _cashService.OpenCashRegisterAsync(model.OpeningAmount, userId);
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
        public IActionResult Close()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(CashCloseViewModel model)
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
                await _cashService.CloseCashRegisterAsync(model.ClosingAmount, userId);
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
                await _cashService.RegisterIncomeAsync(model.Amount, model.Description, userId);
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
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Expense(CashMovementViewModel model)
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
                await _cashService.RegisterExpenseAsync(model.Amount, model.Description, userId);
                TempData["Success"] = "Egreso registrado exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }
    }
}