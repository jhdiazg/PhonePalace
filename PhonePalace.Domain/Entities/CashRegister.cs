using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class CashRegister
    {
        [Key]
        public int CashRegisterID { get; set; }

        [Required]
        [Display(Name = "Fecha de Apertura")]
        public DateTime OpeningDate { get; set; }

        [Required]
        [Display(Name = "Monto Inicial")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal OpeningAmount { get; set; }

        [Required]
        public bool IsOpen { get; set; } = true;

        [Display(Name = "Fecha de Cierre")]
        public DateTime? ClosingDate { get; set; }

        [Display(Name = "Monto Final")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ClosingAmount { get; set; }

        [Display(Name = "Usuario que Abrió")]
        public string? OpenedByUserId { get; set; }

        [Display(Name = "Usuario que Cerró")]
        public string? ClosedByUserId { get; set; }

        public virtual ICollection<CashMovement>? CashMovements { get; set; } = new List<CashMovement>();
    }
}