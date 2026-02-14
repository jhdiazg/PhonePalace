using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.ViewModels;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class BanksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IBankService _bankService;
        private readonly ICashService _cashService;

        public BanksController(ApplicationDbContext context, IAuditService auditService, IBankService bankService, ICashService cashService)
        {
            _context = context;
            _auditService = auditService;
            _bankService = bankService;
            _cashService = cashService;
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

            ViewBag.BankTransactions = await _context.BankTransactions
                .Where(t => t.BankID == id)
                .OrderByDescending(t => t.Date)
                .AsNoTracking()
                .ToListAsync();

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

            // Cargar los movimientos manualmente y pasarlos a la vista mediante ViewBag
            ViewBag.BankTransactions = await _context.BankTransactions
                .Where(t => t.BankID == id)
                .OrderByDescending(t => t.Date)
                .AsNoTracking()
                .ToListAsync();

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

        // GET: Banks/Transfer
        [HttpGet]
        public async Task<IActionResult> Transfer()
        {
            var banks = await _context.Banks.Where(b => b.IsActive).OrderBy(b => b.Name).ToListAsync();
            var model = new BankTransferViewModel
            {
                Banks = new SelectList(banks, "BankID", "Name")
            };
            return View(model);
        }

        // POST: Banks/Transfer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Transfer(BankTransferViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Validaciones lógicas según el tipo
                if (model.TransferType == BankTransferType.BankToBank)
                {
                    if (!model.SourceBankId.HasValue || !model.TargetBankId.HasValue)
                    {
                        ModelState.AddModelError("", "Debe seleccionar banco de origen y destino.");
                    }
                    else if (model.SourceBankId == model.TargetBankId)
                    {
                        ModelState.AddModelError("", "El banco de origen y destino no pueden ser el mismo.");
                    }
                }
                else if (model.TransferType == BankTransferType.BankToCash)
                {
                    if (!model.SourceBankId.HasValue) ModelState.AddModelError("SourceBankId", "Debe seleccionar el banco de origen.");
                }
                else if (model.TransferType == BankTransferType.CashToBank)
                {
                    if (!model.TargetBankId.HasValue) ModelState.AddModelError("TargetBankId", "Debe seleccionar el banco de destino.");
                }

                if (ModelState.IsValid)
                {
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (userId == null) return Unauthorized();

                    try 
                    {
                        // Usamos una transacción global para asegurar que ambos lados (Banco y Caja) se actualicen o ninguno
                        var strategy = _context.Database.CreateExecutionStrategy();
                        return await strategy.ExecuteAsync(async () =>
                        {
                            using var transaction = await _context.Database.BeginTransactionAsync();
                            try
                            {
                                switch (model.TransferType)
                                {
                                    case BankTransferType.BankToBank:
                                        await _bankService.RegisterTransferAsync(model.SourceBankId!.Value, model.TargetBankId!.Value, model.Amount, model.Description);
                                        await _auditService.LogAsync("Bancos", $"Transferencia entre bancos: {model.Amount:C} de ID {model.SourceBankId} a ID {model.TargetBankId}");
                                        break;

                                    case BankTransferType.BankToCash:
                                        // Validar caja abierta
                                        var cashRegister = await _cashService.GetCurrentCashRegisterAsync();
                                        if (cashRegister == null) throw new InvalidOperationException("La caja debe estar abierta para recibir dinero.");

                                        // Retiro del banco
                                        await _bankService.RegisterManualMovementAsync(model.SourceBankId!.Value, Domain.Enums.BankTransactionType.TransferOut, model.Amount, $"Retiro hacia Caja: {model.Description}");
                                        // Ingreso a caja
                                        await _cashService.RegisterIncomeAsync(model.Amount, $"Transferencia desde Banco: {model.Description}", userId);
                                        
                                        await _auditService.LogAsync("Bancos", $"Retiro de banco a caja: {model.Amount:C} del banco ID {model.SourceBankId}");
                                        break;

                                    case BankTransferType.CashToBank:
                                        // Validar caja abierta
                                        var cashRegister2 = await _cashService.GetCurrentCashRegisterAsync();
                                        if (cashRegister2 == null) throw new InvalidOperationException("La caja debe estar abierta para sacar dinero.");

                                        // Egreso de caja
                                        await _cashService.RegisterExpenseAsync(model.Amount, $"Consignación a Banco: {model.Description}", userId);
                                        // Ingreso al banco
                                        await _bankService.RegisterManualMovementAsync(model.TargetBankId!.Value, Domain.Enums.BankTransactionType.TransferIn, model.Amount, $"Consignación desde Caja: {model.Description}");

                                        await _auditService.LogAsync("Bancos", $"Consignación de caja a banco: {model.Amount:C} al banco ID {model.TargetBankId}");
                                        break;
                                }

                                await transaction.CommitAsync();
                                TempData["Success"] = "Transferencia realizada exitosamente.";
                                return RedirectToAction(nameof(Index));
                            }
                            catch
                            {
                                await transaction.RollbackAsync();
                                throw; // Re-lanzar para que el strategy maneje reintentos o falle al catch externo
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                         ModelState.AddModelError("", $"Error: {ex.Message}");
                    }
                }
            }

            // Recargar lista si hay error
            var banks = await _context.Banks.Where(b => b.IsActive).OrderBy(b => b.Name).ToListAsync();
            model.Banks = new SelectList(banks, "BankID", "Name");
            return View(model);
        }
    }
}