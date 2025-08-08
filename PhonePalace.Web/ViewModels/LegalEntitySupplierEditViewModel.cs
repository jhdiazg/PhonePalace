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

        [Required]
        [StringLength(20)]
        [Display(Name = "NIT")]
        public string? NIT { get; set; }

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
