using System.ComponentModel.DataAnnotations;
using PhonePalace.Domain.Enums;

namespace PhonePalace.Web.ViewModels
{
    public class PaymentViewModel
    {
        [Required]
        [Display(Name = "Método de Pago")]
        public string PaymentMethod { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a cero.")]
        [Display(Name = "Monto")]
        public decimal Amount { get; set; }

        [Display(Name = "Número de Referencia")]
        public string? ReferenceNumber { get; set; }

        // Propiedad para identificar el banco en pagos que lo requieran
        public int? BankID { get; set; }
    }
}
