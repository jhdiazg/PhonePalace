﻿using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using PhonePalace.Domain.Enums;

namespace PhonePalace.Domain.Entities
{
    /// <summary>
    /// Representa la clase base abstracta para todos los productos en el inventario.
    /// Utiliza la estrategia de herencia Table-Per-Hierarchy (TPH).
    /// </summary>
    public abstract class Product
    {
        [DisplayName("ID")]
        public int ProductID { get; set; }

        [DisplayName("Nombre")]
        public string Name { get; set; } = string.Empty;
        [DisplayName("Descripción")]
        public string? Description { get; set; }
        [DisplayName("Precio")]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18, 2)")]
        public decimal Price { get; set; }
        [DisplayName("Costo")]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18, 2)")]
        public decimal Cost { get; set; }
        [DisplayName("SKU")]
        [StringLength(50)]
        public string? SKU { get; set; }

        [DisplayName("Código")]
        [StringLength(10)]
        public string? Code { get; set; }

        public int CategoryID { get; set; }
        [DisplayName("Categoría")]
        public virtual Category Category { get; set; } = null!;

        public virtual ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
        public virtual ICollection<Inventory> InventoryLevels { get; set; } = new List<Inventory>();
        [DisplayName("Estado del Producto")]
        public ProductCondition ProductCondition { get; set; }
        [DisplayName("Estado")]
        public bool IsActive { get; set; } = true;
    }
}