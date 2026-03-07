using System.Threading.Tasks;
using PhonePalace.Domain.DTOs;
using PhonePalace.Domain.Entities;

namespace PhonePalace.Domain.Interfaces
{
    public interface ISalesResult
    {
        bool Success { get; }
        string ErrorMessage { get; }
        int? InvoiceId { get; }
    }

    public interface ISalesService
    {
        Task<ISalesResult> ProcessSaleAsync(SaleCreateDto dto, string userId);
    }
}