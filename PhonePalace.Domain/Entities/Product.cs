﻿using System.Collections.Generic;

namespace PhonePalace.Domain.Entities
{
    /// <summary>
    /// Representa la clase base abstracta para todos los productos en el inventario.
    /// Utiliza la estrategia de herencia Table-Per-Hierarchy (TPH).
    /// </summary>
    public abstract class Product
    {
        public int ProductID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public string SKU { get; set; } = string.Empty;

        public int CategoryID { get; set; }
        public virtual Category Category { get; set; } = null!;

        public virtual ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
        public virtual ICollection<Inventory> InventoryLevels { get; set; } = new List<Inventory>();
        
        public bool IsActive { get; set; } = true;
    }
}