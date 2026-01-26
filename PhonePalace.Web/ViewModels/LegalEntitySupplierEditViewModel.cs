using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class LegalEntitySupplierEditViewModel
    {
        public int SupplierID { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Razón Social")]
        public string? CompanyName { get; set; }

        [Required(ErrorMessage = "El número de NIT es obligatorio.")]
        [RegularExpression("^[0-9]+$", ErrorMessage = "El NIT debe contener solo números.")]
        [Display(Name = "Número de NIT")]
        public string? NitNumber { get; set; }

        [Required(ErrorMessage = "El dígito de verificación es obligatorio.")]
        [RegularExpression("^[0-9]$", ErrorMessage = "El dígito de verificación debe ser un solo número.")]
        [Display(Name = "Dígito Verificación")]
        public string? VerificationDigit { get; set; }
        
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

        public bool IsActive { get; set; }
    }
}
