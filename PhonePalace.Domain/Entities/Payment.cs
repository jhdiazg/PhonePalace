using PhonePalace.Domain.Enums;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class Payment
    {
        public int PaymentID { get; set; }
        public int InvoiceID { get; set; }
        [DisplayName("Factura")]
        [Required]
        public virtual Invoice? Invoice { get; set; }
        [DisplayName("Forma de pago")]
        [Required]
        public PaymentMethod PaymentMethod { get; set; }
        [Display(Name = "Monto")]
        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }
        [Display(Name = "Referencia")]
        [StringLength(100)]
        public string? ReferenceNumber { get; set; } // Para tarjetas, transferencias, etc.
        
        // Foreign key para saber a qué cuenta bancaria ingresó el pago
        public int? BankID { get; set; }
        public virtual Bank? Bank { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.Now;
    }
}