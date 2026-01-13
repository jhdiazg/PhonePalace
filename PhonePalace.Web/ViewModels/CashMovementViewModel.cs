using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class CashMovementViewModel
    {
        [Required]
        [Display(Name = "Monto")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0.")]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "Descripción")]
        [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres.")]
        public string Description { get; set; } = string.Empty;
    }
}