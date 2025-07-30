﻿namespace PhonePalace.Domain.Entities
{
    /// <summary>
    /// Representa un producto de tipo Teléfono Celular.
    /// </summary>
    public class CellPhone : Product
    {
        public int ModelID { get; set; }
        public virtual Model Model { get; set; } = null!; // El modelo ya contiene la marca (Brand)
        public string? Color { get; set; } = string.Empty;
        public int StorageGB { get; set; }
        public int RamGB { get; set; }
    }
}