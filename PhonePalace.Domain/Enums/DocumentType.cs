﻿using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Enums
{
    public enum DocumentType
    {
        [Display(Name = "Cédula de Ciudadanía")]
        CitizenshipCard,
        [Display(Name = "Cédula de Extranjería")]
        ForeignerId,
        [Display(Name = "Pasaporte")]
        Passport,
        [Display(Name = "Otro")]
        Other
    }
}