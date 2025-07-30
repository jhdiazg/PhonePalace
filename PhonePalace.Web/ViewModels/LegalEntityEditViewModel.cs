﻿using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class LegalEntityEditViewModel : IAddressViewModel
    {
        public int ClientID { get; set; }

        [Required(ErrorMessage = "La razón social es obligatoria.")]
        [StringLength(200)]
        [Display(Name = "Razón Social")]
        public string CompanyName { get; set; } = string.Empty;

        // En LegalEntityEditViewModel.cs
        public string DisplayName => CompanyName;


        [Required(ErrorMessage = "El NIT es obligatorio.")]
        [StringLength(20)]
        [Display(Name = "NIT")]
        public string NIT { get; set; } = string.Empty;

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