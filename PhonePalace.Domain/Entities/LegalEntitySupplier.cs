using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Entities
{
    public class LegalEntitySupplier : Supplier
    {
        [Required]
        [StringLength(200)]
        [Display(Name = "Razón Social")]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        [Display(Name = "NIT")]
        public string NIT { get; set; } = string.Empty;

        public override string DisplayName => CompanyName;
    }
}