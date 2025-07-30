using PhonePalace.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class NaturalPerson : Client
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(50)]
        [Display(Name = "Nombres")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es obligatorio.")]
        [StringLength(50)]
        [Display(Name = "Apellidos")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El tipo de documento es obligatorio.")]
        [Display(Name = "Tipo de Documento")]
        public DocumentType DocumentType { get; set; }

        [Required(ErrorMessage = "El número de documento es obligatorio.")]
        [StringLength(20)]
        [Display(Name = "Número de Documento")]
        public string DocumentNumber { get; set; } = string.Empty;

        public override string DisplayName => $"{FirstName} {LastName}";
    }
}