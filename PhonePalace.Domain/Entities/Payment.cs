using PhonePalace.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class Payment
    {
        public int PaymentID { get; set; }
        public int InvoiceID { get; set; }
        public virtual Invoice Invoice { get; set; }

        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        [StringLength(100)]
        public string? ReferenceNumber { get; set; } // Para tarjetas, transferencias, etc.
    }
}