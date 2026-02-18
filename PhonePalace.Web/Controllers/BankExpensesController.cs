// d:\PhonePalace\PhonePalace.Web\Controllers\BankExpensesController.cs

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Administrador,Cajero")]
    public class BankExpensesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBankService _bankService;
        private readonly IAuditService _auditService;

        public BankExpensesController(ApplicationDbContext context, IBankService bankService, IAuditService auditService)
        {
            _context = context;
            _bankService = bankService;
            _auditService = auditService;
        }

        // GET: BankExpenses/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Banks = new SelectList(await _context.Banks.Where(b => b.IsActive).ToListAsync(), "BankID", "Name");
            return View();
        }

        // POST: BankExpenses/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int bankId, decimal amount, string observation)
        {
            if (amount <= 0) ModelState.AddModelError("amount", "El monto debe ser mayor a cero.");
            if (string.IsNullOrWhiteSpace(observation)) ModelState.AddModelError("observation", "La observación es obligatoria.");

            // Validar que el banco tenga saldo suficiente
            var bank = await _context.Banks.AsNoTracking().FirstOrDefaultAsync(b => b.BankID == bankId);
            if (bank != null && bank.Balance < amount)
            {
                ModelState.AddModelError("amount", $"Saldo insuficiente. El banco tiene {bank.Balance:C} disponible.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Banks = new SelectList(await _context.Banks.Where(b => b.IsActive).ToListAsync(), "BankID", "Name", bankId);
                return View();
            }

            try
            {
                // Registrar el egreso bancario como Gasto Operativo (ManualExpense)
                // Esto descontará el saldo del banco inmediatamente.
                await _bankService.RegisterManualMovementAsync(bankId, BankTransactionType.ManualExpense, amount, $"GASTO OPERATIVO: {observation.ToUpper()}");
                
                await _auditService.LogAsync("Bancos", $"Registró gasto operativo por {amount:C} en banco ID {bankId}. Obs: {observation}");
                
                TempData["Success"] = "Gasto bancario registrado exitosamente.";
                
                // Redirigir al reporte de movimientos bancarios para ver el egreso reflejado
                return RedirectToAction("BankTransactions", "Reports", new { bankId = bankId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                ViewBag.Banks = new SelectList(await _context.Banks.Where(b => b.IsActive).ToListAsync(), "BankID", "Name", bankId);
                return View();
            }
        }
    }
}
