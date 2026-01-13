using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class CashCloseViewModel
    {
        [Required]
        [Display(Name = "Monto Final Contado")]
        [Range(0, double.MaxValue, ErrorMessage = "El monto debe ser mayor o igual a 0.")]
        public decimal ClosingAmount { get; set; }
    }
}