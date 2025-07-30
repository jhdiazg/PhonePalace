using PhonePalace.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class NaturalPersonEditViewModel : IAddressViewModel
    {
        public int ClientID { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100)]
        [Display(Name = "Nombres")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es obligatorio.")]
        [StringLength(100)]
        [Display(Name = "Apellidos")]
        public string LastName { get; set; } = string.Empty;

        // En NaturalPersonEditViewModel.cs
        public string DisplayName => $"{FirstName} {LastName}";
        
        [Required(ErrorMessage = "El tipo de documento es obligatorio.")]
        [Display(Name = "Tipo de Documento")]
        public DocumentType DocumentType { get; set; }

        [Required(ErrorMessage = "El número de documento es obligatorio.")]
        [StringLength(20)]
        [Display(Name = "Número de Documento")]
        public string DocumentNumber { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "Correo Electrónico")]
        public string? Email { get; set; }

        [Display(Name = "Teléfono")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Departamento")]
        public string? DepartmentID { get; set; }

        [Display(Name = "Municipio")]
        public string? MunicipalityID { get; set; }

        [StringLength(200)]
        [Display(Name = "Dirección")]
        public string? StreetAddress { get; set; }

        [Display(Name = "Activo")]
        public bool IsActive { get; set; }
    }
}