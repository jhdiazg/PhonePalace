﻿namespace PhonePalace.Domain.Entities
{
    /// <summary>
    /// Representa un producto de tipo Accesorio (cables, carcasas, cargadores, etc.).
    /// </summary>
    public class Accessory : Product
    {
        // Un accesorio puede tener su propia marca (ej. Anker, Spigen)
        public int? BrandID { get; set; }
        public virtual Brand? Brand { get; set; }

        public string? Color { get; set; }
        public string? Material { get; set; }
        public string? Compatibility { get; set; } // Ej: "Para iPhone 15", "Universal USB-C"
    }
}