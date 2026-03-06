using System.Threading.Tasks;
using PhonePalace.Domain.Entities;

namespace PhonePalace.Domain.Interfaces
{
    public interface IPlemsiService
    {
        Task<PlemsiResponse> SendInvoiceAsync(Sale sale, int electronicInvoiceNumber);
        Task<PlemsiResponse> GetInvoiceStatusAsync(int invoiceId);
        Task<PlemsiResponse> SendCreditNoteAsync(Sale sale, string reason, string originalCufe, int? invoiceNumber = null);
        Task<PlemsiResponse> SendPartialCreditNoteAsync(Return returnEntity, Sale sale, string reason, string originalCufe, int? invoiceNumber = null);
    }

    public class PlemsiResponse
    {
        public bool Success { get; set; }
        public string? Cufe { get; set; }
        public string? Number { get; set; } // Número completo con prefijo
        public string? QrUrl { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Status { get; set; } // "Accepted", "Rejected"
    }
}
