using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public enum BankAdjustmentType
    {
        [Display(Name = "Ingreso (Aumentar Saldo)")]
        Income,
        [Display(Name = "Egreso (Disminuir Saldo)")]
        Expense
    }

    public class BankAdjustViewModel
    {
        public int BankId { get; set; }
        
        [Display(Name = "Banco")]
        public string BankName { get; set; } = string.Empty;
        
        [Display(Name = "Saldo Actual")]
        [DataType(DataType.Currency)]
        public decimal CurrentBalance { get; set; }

        [Required(ErrorMessage = "Seleccione el tipo de ajuste.")]
        [Display(Name = "Tipo de Ajuste")]
        public BankAdjustmentType AdjustmentType { get; set; }

        [Required(ErrorMessage = "El monto es requerido.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a cero.")]
        [DataType(DataType.Currency)]
        [Display(Name = "Monto del Ajuste")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "El motivo es requerido.")]
        [Display(Name = "Motivo / Descripción")]
        public string Description { get; set; } = string.Empty;
    }
}