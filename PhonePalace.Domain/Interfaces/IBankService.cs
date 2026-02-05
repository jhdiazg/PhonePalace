using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using System.Threading.Tasks;

namespace PhonePalace.Domain.Interfaces
{
    public interface IBankService
    {
        Task RegisterIncomeFromPaymentAsync(Payment payment);
        Task RegisterTransferAsync(int sourceBankId, int targetBankId, decimal amount, string description);
        Task RegisterManualMovementAsync(int bankId, BankTransactionType type, decimal amount, string description);
    }
}