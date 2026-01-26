using PhonePalace.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class SupplierCreateViewModel
    {
        [Required]
        [Display(Name = "Tipo de Proveedor")]
        public SupplierTypeSelection SupplierType { get; set; }

        // Properties for NaturalPersonSupplier
        [Display(Name = "Nombres")]
        public string? FirstName { get; set; }

        [Display(Name = "Apellidos")]
        public string? LastName { get; set; }

        [Display(Name = "Tipo de Documento")]
        public DocumentType? DocumentType { get; set; }

        [Display(Name = "Número de Documento")]
        public string? DocumentNumber { get; set; }

        // Properties for LegalEntitySupplier
        [Display(Name = "Razón Social")]
        public string? CompanyName { get; set; }

        [Display(Name = "NIT")]
        [RegularExpression("^[0-9]+$", ErrorMessage = "El NIT debe contener solo números.")]
        public string? NitNumber { get; set; }

        [Display(Name = "Dígito Verificación")]
        [RegularExpression("^[0-9]$", ErrorMessage = "El dígito de verificación debe ser un solo número.")]
        public string? VerificationDigit { get; set; }

        // Common Properties
        [EmailAddress]
        [StringLength(100)]
        [Display(Name = "Correo Electrónico")]
        public string? Email { get; set; }

        [StringLength(20)]
        [Display(Name = "Teléfono")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Departamento")]
        public string? DepartmentID { get; set; }

        [Display(Name = "Municipio")]
        public string? MunicipalityID { get; set; }

        [StringLength(200)]
        [Display(Name = "Dirección (Calle, N°, etc.)")]
        public string? StreetAddress { get; set; }
    }

    public enum SupplierTypeSelection
    {
        NaturalPerson,
        LegalEntity
    }
}
