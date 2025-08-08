﻿using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhonePalace.Domain.Enums;

namespace PhonePalace.Domain.Entities
{
    /// <summary>
    /// Representa un producto de tipo Accesorio (cables, carcasas, cargadores, etc.).
    /// </summary>
    public class Accessory : Product
    {
        // Un accesorio puede tener su propia marca (ej. Anker, Spigen)
        public int? BrandID { get; set; }
        [DisplayName("Marca")]
        public virtual Brand? Brand { get; set; }

        [Column("Accessory_Color")]
        public string? Color { get; set; }
        public string? Material { get; set; }

        [Display(Name = "Compatibilidad")]
        public string? Compatibility { get; set; } // Ej: "Para iPhone 15", "Universal USB-C"
    }
} 