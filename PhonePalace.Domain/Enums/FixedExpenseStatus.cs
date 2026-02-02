using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Enums
{
    public enum FixedExpenseStatus
    {
        [Display(Name = "Pendiente")]
        Pending,

        [Display(Name = "Pagado")]
        Paid,

        [Display(Name = "Anulado")]
        Cancelled
    }
}