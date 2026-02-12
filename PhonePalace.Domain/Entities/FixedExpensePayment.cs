using PhonePalace.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class FixedExpensePayment
    {
        [Key]
        public int Id { get; set; }

        public int FixedExpenseId { get; set; }
        [ForeignKey("FixedExpenseId")]
        public virtual FixedExpense? FixedExpense { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; }

        [Required]
        public DateTime Period { get; set; } // Representa el mes/año pagado (ej: 01/11/2023)

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        public PaymentMethod PaymentMethod { get; set; }

        [StringLength(200)]
        public string? Notes { get; set; }

        public int? CashMovementId { get; set; }
        public virtual CashMovement? CashMovement { get; set; }
    }
}