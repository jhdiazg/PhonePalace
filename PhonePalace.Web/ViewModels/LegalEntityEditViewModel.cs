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


        [Required(ErrorMessage = "El número de NIT es obligatorio.")]
        [RegularExpression("^[0-9]+$", ErrorMessage = "El NIT debe contener solo números.")]
        [Display(Name = "Número de NIT")]
        public string? NitNumber { get; set; }

        [Required(ErrorMessage = "El dígito de verificación es obligatorio.")]
        [RegularExpression("^[0-9]$", ErrorMessage = "El dígito de verificación debe ser un solo número.")]
        [Display(Name = "Dígito Verificación")]
        public string? VerificationDigit { get; set; }
        
        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
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