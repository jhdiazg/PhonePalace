using PhonePalace.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Entities
{
    public class NaturalPersonSupplier : Supplier
    {
        [Required]
        [StringLength(100)]
        [Display(Name = "Nombres")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Apellidos")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Tipo de Documento")]
        public DocumentType? DocumentType { get; set; }
        

        [Required]
        [StringLength(20)]
        [Display(Name = "Número de Documento")]
        public string DocumentNumber { get; set; } = string.Empty;

        public override string DisplayName => $"{FirstName} {LastName}";
    }
}