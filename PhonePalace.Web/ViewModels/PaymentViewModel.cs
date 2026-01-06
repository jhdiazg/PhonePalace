using System.ComponentModel.DataAnnotations;
using PhonePalace.Domain.Enums;

namespace PhonePalace.Web.ViewModels
{
    public class PaymentViewModel
    {
        [Required]
        [Display(Name = "Método de Pago")]
        public PaymentMethod PaymentMethod { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a cero.")]
        [Display(Name = "Monto")]
        public decimal Amount { get; set; }

        [Display(Name = "Número de Referencia")]
        public string? ReferenceNumber { get; set; }
    }
}
