using PhonePalace.Domain.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class FixedExpensePayViewModel
    {
        public int Id { get; set; }
        public string Concept { get; set; } = string.Empty;
        
        public int Year { get; set; }
        public int Month { get; set; }

        [Required]
        [Display(Name = "Monto a Pagar")]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "Fecha de Pago")]
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        [Required]
        [Display(Name = "Forma de Pago")]
        public PaymentMethod PaymentMethod { get; set; }

        [Display(Name = "Notas / Observaciones")]
        public string? Notes { get; set; }
    }
}