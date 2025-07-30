﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class LegalEntity : Client
    {
        [Required(ErrorMessage = "La razón social es obligatoria.")]
        [StringLength(150)]
        [Display(Name = "Razón Social")]
        public string CompanyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El NIT es obligatorio.")]
        [StringLength(20)]
        [Display(Name = "NIT")]
        public string NIT { get; set; } = string.Empty;

        public override string DisplayName => CompanyName;
    }
}