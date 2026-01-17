using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Enums
{
    public enum AccountPayableDocumentType
    {
        [Display(Name = "Factura")]
        Invoice,
        [Display(Name = "Cuenta de Cobro")]
        Bill,
        [Display(Name = "Recibo")]
        Receipt,
        [Display(Name = "Otro")]
        Other
    }
}