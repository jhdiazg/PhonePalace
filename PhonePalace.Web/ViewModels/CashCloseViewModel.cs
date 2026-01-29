using System;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class CashCloseViewModel
    {
        [Display(Name = "Dinero en Caja (Conteo Físico)")]
        [Required(ErrorMessage = "El monto de cierre es obligatorio.")]
        [Range(0, double.MaxValue, ErrorMessage = "El monto debe ser mayor o igual a 0.")]
        public decimal ClosingAmount { get; set; }

        public decimal SystemBalance { get; set; } // Saldo calculado por el sistema (informativo)
        public string? Observation { get; set; }
    }
}