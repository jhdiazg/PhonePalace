using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Enums
{
    public enum CashMovementType
    {
        [Display(Name = "Ingreso")]
        Income,
        [Display(Name = "Egreso")]
        Expense,
        [Display(Name = "Apertura")]
        Opening,
        [Display(Name = "Cierre")]
        Closing
    }
}