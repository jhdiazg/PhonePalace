using PhonePalace.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class NaturalPersonSupplierEditViewModel
    {
        public int SupplierID { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Nombres")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Apellidos")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Tipo de Documento")]
        public DocumentType DocumentType { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Número de Documento")]
        public string DocumentNumber { get; set; } = string.Empty;

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
