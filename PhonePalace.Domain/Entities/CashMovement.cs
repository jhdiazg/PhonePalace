using PhonePalace.Domain.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class CashMovement
    {
        [Key]
        public int CashMovementID { get; set; }

        [Required]
        public int CashRegisterID { get; set; }
        public virtual CashRegister? CashRegister { get; set; }

        [Required]
        [Display(Name = "Tipo de Movimiento")]
        public CashMovementType MovementType { get; set; }

        [Required]
        [Display(Name = "Monto")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "Descripción")]
        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Fecha del Movimiento")]
        public DateTime MovementDate { get; set; } = DateTime.UtcNow;

        // Para integración con pagos
        public int? PaymentID { get; set; }
        public Payment? Payment { get; set; }

        // Usuario que registró
        public string? UserId { get; set; }
    }
}