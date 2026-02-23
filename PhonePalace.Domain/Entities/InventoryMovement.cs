using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhonePalace.Domain.Enums;

namespace PhonePalace.Domain.Entities
{
    public class InventoryMovement
    {
        [Key]
        public int Id { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }

        public InventoryMovementType Type { get; set; }
        
        public int Quantity { get; set; } // Positivo (Entrada) o Negativo (Salida)
        
        [Column(TypeName = "decimal(18, 2)")]
        public decimal UnitCost { get; set; } // Costo en el momento del movimiento
        
        public int StockBalance { get; set; } // Stock resultante después del movimiento
        
        public string? Reference { get; set; } // Ej: "Venta #123", "Ajuste por robo"
        public string? UserId { get; set; }
    }
}