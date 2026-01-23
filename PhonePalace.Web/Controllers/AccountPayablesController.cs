using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Web.Helpers;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace PhonePalace.Web.Controllers
{
    public class AccountPayablesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public AccountPayablesController(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        public async Task<IActionResult> Index(int? pageNumber, int? pageSize)
        {
            ViewData["PageSize"] = pageSize ?? 10;

            var accountPayablesQuery = _context.AccountPayables
                .AsNoTracking() // Improves performance for read-only queries
                .Include(a => a.Purchase!)
                .ThenInclude(p => p!.Supplier);

            var accountPayables = await PaginatedList<AccountPayable>.CreateAsync(accountPayablesQuery.OrderByDescending(a => a.CreatedDate), pageNumber ?? 1, pageSize ?? 10);
            return View(accountPayables);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var accountPayable = await _context.AccountPayables
                .Include(a => a.Purchase)
                // .ThenInclude(p => p!.Supplier)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (accountPayable == null) return NotFound();

            return View(accountPayable);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("DocumentType,DocumentNumber,Amount,DueDate")] AccountPayable accountPayable)
        {
            // Regla de negocio: El estado inicial no puede ser Pagada
            accountPayable.IsPaid = false;
            accountPayable.CreatedDate = DateTime.Now;
            accountPayable.DocumentNumber = accountPayable.DocumentNumber?.ToUpper();

            if (ModelState.IsValid)
            {
                _context.Add(accountPayable);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Cuentas por Pagar", $"Creó la cuenta por pagar manual #{accountPayable.Id}.");
                return RedirectToAction(nameof(Index));
            }
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
        public async Task<IActionResult> Edit(int id, [Bind("Id,PurchaseId,DocumentType,DocumentNumber,Amount,CreatedDate,DueDate,IsPaid")] AccountPayable accountPayable)
        {
            if (id != accountPayable.Id) return NotFound();

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
    }
}