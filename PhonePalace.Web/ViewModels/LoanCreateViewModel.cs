using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class LoanCreateViewModel
    {
        [Required(ErrorMessage = "El cliente es obligatorio.")]
        public int ClientID { get; set; }

        [Required]
        [Range(1, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "La descripción es obligatoria.")]
        public required string Description { get; set; }

        [Required]
        public DateTime Date { get; set; } = DateTime.Now;

        [Display(Name = "Es Ingreso (No genera salida de dinero)")]
        public bool IsIncomeGenerating { get; set; }
    }
}
