using System;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class CashOpenViewModel
    {
        [Display(Name = "Monto de Apertura")]
        [Required(ErrorMessage = "El monto de apertura es obligatorio.")]
        [Range(0, double.MaxValue, ErrorMessage = "El monto debe ser mayor o igual a 0.")]
        public decimal OpeningBalance { get; set; }
    }
}