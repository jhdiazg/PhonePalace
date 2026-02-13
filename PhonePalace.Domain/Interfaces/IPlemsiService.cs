using System.Threading.Tasks;
using PhonePalace.Domain.Entities;

namespace PhonePalace.Domain.Interfaces
{
    public interface IPlemsiService
    {
        Task<PlemsiResponse> SendInvoiceAsync(Sale sale);
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
