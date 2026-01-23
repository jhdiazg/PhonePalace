﻿using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public abstract class Client
    {
        public int ClientID { get; set; }

        [StringLength(20)]
        [Display(Name = "Teléfono")]
        public string? PhoneNumber { get; set; }

        [EmailAddress]
        [StringLength(100)]
        [Display(Name = "Correo Electrónico")]
        public string? Email { get; set; }
        
        // --- Campos de Dirección Estructurada ---
        [Display(Name = "Departamento")]
        public string? DepartmentID { get; set; }

        [Display(Name = "Municipio")]
        public string? MunicipalityID { get; set; }

        [StringLength(200)]
        [Display(Name = "Dirección (Calle, N°, etc.)")]
        public string? StreetAddress { get; set; }

        public virtual Department? Department { get; set; }
        public virtual Municipality? Municipality { get; set; }

        // Propiedad de borrado lógico
        [Display(Name = "Activo")]
        public bool IsActive { get; set; } = true;

        [NotMapped]
        [Display(Name = "Nombre / Razón Social")]
        public abstract string DisplayName { get; }
    }
}