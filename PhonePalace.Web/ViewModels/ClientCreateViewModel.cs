using PhonePalace.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace PhonePalace.Web.ViewModels
{
    public enum ClientTypeSelection
    {
        [Display(Name = "Persona Natural")]
        NaturalPerson,
        [Display(Name = "Persona Jurídica")]
        LegalEntity
    }

    public class ClientCreateViewModel : IValidatableObject
    {
        [Required]
        [Display(Name = "Tipo de Cliente")]
        public ClientTypeSelection ClientType { get; set; }

        // --- Propiedades Persona Natural ---
        [Display(Name = "Nombres")]
        public string? FirstName { get; set; }
        [Display(Name = "Apellidos")]
        public string? LastName { get; set; }
        [Display(Name = "Tipo de Documento")]
        public DocumentType? DocumentType { get; set; }
        [Display(Name = "Número de Documento")]
        public string? DocumentNumber { get; set; }

        // --- Propiedades Persona Jurídica ---
        [Display(Name = "Razón Social")]
        public string? CompanyName { get; set; }
        [Display(Name = "NIT")]
        [StringLength(9)]
        [RegularExpression("^[0-9]+$", ErrorMessage = "El NIT debe contener solo números.")]
        public string? NitNumber { get; set; }

        [Display(Name = "Dígito Verificación")]
        [StringLength(1)]
        [RegularExpression("^[0-9]$", ErrorMessage = "El dígito de verificación debe ser un solo número.")]
        public string? VerificationDigit { get; set; }
        
        // --- Propiedades Comunes ---
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
        [Display(Name = "Dirección (Calle, N°, etc.)")]
        public string? StreetAddress { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ClientType == ClientTypeSelection.NaturalPerson)
            {
                if (string.IsNullOrWhiteSpace(FirstName))
                    yield return new ValidationResult("El nombre es obligatorio.", new[] { nameof(FirstName) });
                if (string.IsNullOrWhiteSpace(LastName))
                    yield return new ValidationResult("El apellido es obligatorio.", new[] { nameof(LastName) });
                if (DocumentType == null)
                    yield return new ValidationResult("El tipo de documento es obligatorio.", new[] { nameof(DocumentType) });
                if (string.IsNullOrWhiteSpace(DocumentNumber))
                    yield return new ValidationResult("El número de documento es obligatorio.", new[] { nameof(DocumentNumber) });
            }
            else if (ClientType == ClientTypeSelection.LegalEntity)
            {
                if (string.IsNullOrWhiteSpace(CompanyName))
                    yield return new ValidationResult("La razón social es obligatoria.", new[] { nameof(CompanyName) });
                if (string.IsNullOrWhiteSpace(NitNumber))
                    yield return new ValidationResult("El número de NIT es obligatorio.", new[] { nameof(NitNumber) });
                if (string.IsNullOrWhiteSpace(VerificationDigit))
                    yield return new ValidationResult("El dígito de verificación es obligatorio.", new[] { nameof(VerificationDigit) });
            }
        }
    }
}