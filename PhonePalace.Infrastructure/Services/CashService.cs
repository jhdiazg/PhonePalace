using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;
using System.Linq;
using System.Threading.Tasks;

namespace PhonePalace.Infrastructure.Services
{
    public class CashService : ICashService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public CashService(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        public async Task<CashRegister?> GetCurrentCashRegisterAsync()
        {
            return await _context.CashRegisters
                .Include(cr => cr.CashMovements)
                .FirstOrDefaultAsync(cr => cr.IsOpen);
        }

        public async Task<CashRegister> OpenCashRegisterAsync(decimal openingAmount, string userId)
        {
            // Verificar si ya hay una caja abierta
            var existingOpen = await GetCurrentCashRegisterAsync();
            if (existingOpen != null)
            {
                throw new InvalidOperationException("Ya hay una caja abierta.");
            }

            var cashRegister = new CashRegister
            {
                OpeningDate = DateTime.Now,
                OpeningAmount = openingAmount,
                OpenedByUserId = userId,
                IsOpen = true
            };

            _context.CashRegisters.Add(cashRegister);
            await _context.SaveChangesAsync();

            // Registrar movimiento de apertura
            var openingMovement = new CashMovement
            {
                CashRegisterID = cashRegister.CashRegisterID,
                MovementType = CashMovementType.Opening,
                Amount = openingAmount,
                Description = "Apertura de caja",
                UserId = userId,
                MovementDate = DateTime.Now
            };
            _context.CashMovements.Add(openingMovement);
            await _context.SaveChangesAsync();

            try
            {
                await _auditService.LogAsync("Caja", $"Apertura de caja con monto inicial: {openingAmount:C}");
            }
            catch
            {
                // Log error silently
            }

            return cashRegister;
        }

        public async Task CloseCashRegisterAsync(decimal closingAmount, string userId)
        {
            var cashRegister = await GetCurrentCashRegisterAsync();
            if (cashRegister == null)
            {
                throw new InvalidOperationException("No hay caja abierta para cerrar.");
            }

            cashRegister.ClosingDate = DateTime.Now;
            cashRegister.ClosingAmount = closingAmount;
            cashRegister.ClosedByUserId = userId;
            cashRegister.IsOpen = false;

            _context.CashRegisters.Update(cashRegister);

            // Registrar movimiento de cierre
            var closingMovement = new CashMovement
            {
                CashRegisterID = cashRegister.CashRegisterID,
                MovementType = CashMovementType.Closing,
                Amount = closingAmount,
                Description = "Cierre de caja",
                UserId = userId,
                MovementDate = DateTime.Now
            };
            _context.CashMovements.Add(closingMovement);

            await _context.SaveChangesAsync();

            try
            {
                await _auditService.LogAsync("Caja", $"Cierre de caja con monto final: {closingAmount:C}");
            }
            catch
            {
                // Log error silently
            }

            return;
        }

        public async Task<CashMovement> RegisterIncomeAsync(decimal amount, string description, string userId, int? paymentId = null)
        {
            var cashRegister = await GetCurrentCashRegisterAsync();
            if (cashRegister == null)
            {
                throw new InvalidOperationException("No hay caja abierta para registrar movimientos.");
            }

            var movement = new CashMovement
            {
                CashRegisterID = cashRegister.CashRegisterID,
                MovementType = CashMovementType.Income,
                Amount = amount,
                Description = description,
                PaymentID = paymentId,
                UserId = userId,
                MovementDate = DateTime.Now
            };

            _context.CashMovements.Add(movement);
            await _context.SaveChangesAsync();

            try
            {
                await _auditService.LogAsync("Caja", $"Registro de ingreso: {description} - {amount:C}");
            }
            catch
            {
                // Log error silently
            }

            return movement;
        }

        public async Task<CashMovement> RegisterExpenseAsync(decimal amount, string description, string userId)
        {
            var cashRegister = await GetCurrentCashRegisterAsync();
            if (cashRegister == null)
            {
                throw new InvalidOperationException("No hay caja abierta para registrar movimientos.");
            }

            var movement = new CashMovement
            {
                CashRegisterID = cashRegister.CashRegisterID,
                MovementType = CashMovementType.Expense,
                Amount = amount,
                Description = description,
                UserId = userId,
                MovementDate = DateTime.Now
            };

            _context.CashMovements.Add(movement);
            await _context.SaveChangesAsync();

            try
            {
                await _auditService.LogAsync("Caja", $"Registro de egreso: {description} - {amount:C}");
            }
            catch
            {
                // Log error silently
            }

            return movement;
        }

        public async Task<decimal> GetCurrentBalanceAsync()
        {
            var cashRegister = await GetCurrentCashRegisterAsync();
            if (cashRegister == null)
            {
                return 0;
            }

            var totalMovements = await _context.CashMovements
                .Where(cm => cm.CashRegisterID == cashRegister.CashRegisterID)
                .SumAsync(cm => cm.MovementType == CashMovementType.Income ? cm.Amount : -cm.Amount);

            return cashRegister.OpeningAmount + totalMovements;
        }
    }
}