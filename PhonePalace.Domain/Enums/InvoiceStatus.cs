using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Enums
{
    public enum InvoiceStatus
    {
        [Display(Name = "Pendiente")]
        Pending,
        [Display(Name = "Completado")]
        Completed,
        [Display(Name = "Cancelada")]
        Cancelled
    }
}