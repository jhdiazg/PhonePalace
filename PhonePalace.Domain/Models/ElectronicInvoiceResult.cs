// d:\PhonePalace\PhonePalace.Domain\Models\ElectronicInvoiceResult.cs
namespace PhonePalace.Domain.Models
{
    public class ElectronicInvoiceResult
    {
        public bool IsSuccess { get; set; }
        public string? Cufe { get; set; }
        public string? Number { get; set; }
        public string? QrUrl { get; set; }
        public string? XmlUrl { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
