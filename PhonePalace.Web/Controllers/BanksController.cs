using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class BanksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public BanksController(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        // GET: Banks
        public async Task<IActionResult> Index()
        {
            return View(await _context.Banks.AsNoTracking().ToListAsync());
        }

        // GET: Banks/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var bank = await _context.Banks
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.BankID == id);
            if (bank == null)
            {
                return NotFound();
            }

            return View(bank);
        }

        // GET: Banks/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Banks/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,IsActive,AccountNumber")] Bank bank)
        {
            if (ModelState.IsValid)
            {
                // if (bank.Name != null)
                {
                    bank.Name = bank.Name.ToUpper();
                }
                _context.Add(bank);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Bancos", $"Creó el banco '{bank.Name}' (ID: {bank.BankID}).");
                return RedirectToAction(nameof(Index));
            }
            return View(bank);
        }

        // GET: Banks/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var bank = await _context.Banks.FindAsync(id);
            if (bank == null)
            {
                return NotFound();
            }
            return View(bank);
        }

        // POST: Banks/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("BankID,Name,IsActive,AccountNumber")] Bank bank)
        {
            if (id != bank.BankID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (!string.IsNullOrEmpty(bank.Name))
                    {
                        bank.Name = bank.Name.ToUpper();
                    }
                    _context.Update(bank);
                    await _context.SaveChangesAsync();
                    await _auditService.LogAsync("Bancos", $"Editó el banco '{bank.Name}' (ID: {bank.BankID}).");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BankExists(bank.BankID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(bank);
        }

        // GET: Banks/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var bank = await _context.Banks
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.BankID == id);
            if (bank == null)
            {
                return NotFound();
            }

            return View(bank);
        }

        // POST: Banks/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bank = await _context.Banks.FindAsync(id);
            if (bank != null)
            {
                bank.IsActive = false;
                _context.Update(bank);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Bancos", $"Eliminó el banco '{bank.Name}' (ID: {bank.BankID}).");
            }
            
            return RedirectToAction(nameof(Index));
        }

        private bool BankExists(int id)
        {
            return _context.Banks.Any(e => e.BankID == id);
        }
    }
}