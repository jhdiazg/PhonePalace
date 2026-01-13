using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class CashOpenViewModel
    {
        [Required]
        [Display(Name = "Monto Inicial")]
        [Range(0, double.MaxValue, ErrorMessage = "El monto debe ser mayor o igual a 0.")]
        public decimal OpeningAmount { get; set; }
    }
}