using PhonePalace.Domain.Entities;
using System.Threading.Tasks;

namespace PhonePalace.Domain.Interfaces
{
    public interface ICashService
    {
        Task<CashRegister?> GetCurrentCashRegisterAsync();
        Task<CashRegister> OpenCashRegisterAsync(decimal openingAmount, string userId);
        Task CloseCashRegisterAsync(decimal closingAmount, string userId);
        Task<CashMovement> RegisterIncomeAsync(decimal amount, string description, string userId, int? paymentId = null);
        Task<CashMovement> RegisterExpenseAsync(decimal amount, string description, string userId);
        Task<decimal> GetCurrentBalanceAsync();
    }
}