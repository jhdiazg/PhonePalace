using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Enums
{
    public enum VerificationStatus
    {
        [Display(Name = "Pendiente")]
        Pending,
        [Display(Name = "Verificado")]
        Verified,
        [Display(Name = "Rechazado")]
        Rejected
    }
}
