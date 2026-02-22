using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class PublicClientRegisterViewModel
    {
        [Required(ErrorMessage = "El correo es obligatorio")]
        [EmailAddress]
        [Display(Name = "Correo Electrónico")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [StringLength(100, ErrorMessage = "La {0} debe tener al menos {2} caracteres.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirmar contraseña")]
        [Compare("Password", ErrorMessage = "Las contraseñas no coinciden.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        // Datos de Facturación
        [Required(ErrorMessage = "Seleccione el tipo de persona")]
        [Display(Name = "Tipo de Persona")]
        public ClientTypeSelection ClientType { get; set; }

        // Persona Natural
        [Display(Name = "Nombres")]
        public string? FirstName { get; set; }
        [Display(Name = "Apellidos")]
        public string? LastName { get; set; }
        [Display(Name = "Tipo Documento")]
        public PhonePalace.Domain.Enums.DocumentType? DocumentType { get; set; }
        [Display(Name = "Número Documento")]
        public string? DocumentNumber { get; set; }

        // Persona Jurídica
        [Display(Name = "Razón Social")]
        public string? CompanyName { get; set; }
        [Display(Name = "NIT")]
        public string? NitNumber { get; set; }
        [Display(Name = "DV")]
        public string? VerificationDigit { get; set; }

        [Required(ErrorMessage = "El teléfono es obligatorio")]
        [Display(Name = "Teléfono / Celular")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "La dirección es obligatoria")]
        [Display(Name = "Dirección Física")]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "El departamento es obligatorio")]
        [Display(Name = "Departamento")]
        public string DepartmentID { get; set; } = string.Empty;

        [Required(ErrorMessage = "El municipio es obligatorio")]
        [Display(Name = "Municipio")]
        public string MunicipalityID { get; set; } = string.Empty;

        [Display(Name = "Autorización de Datos")]
        public bool DataAuthorization { get; set; } 
    }
}