using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class OpenCashViewModel
    {
        [Required(ErrorMessage = "El monto de apertura es obligatorio.")]
        [DataType(DataType.Currency)]
        [Range(0, double.MaxValue, ErrorMessage = "El monto debe ser un valor positivo.")]
        [Display(Name = "Monto de Apertura")]
        public decimal OpeningAmount { get; set; }
    }
}