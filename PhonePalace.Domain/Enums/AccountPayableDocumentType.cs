using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Enums
{
    public enum AccountPayableDocumentType
    {
        [Display(Name = "Factura")]
        Invoice,
        [Display(Name = "Remisión")]
        Remittance,
        [Display(Name = "Otro")]
        Other
    }
}