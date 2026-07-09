using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class SupplierBalanceMovement
    {
        [Key]
        public int Id { get; set; }
        public int SupplierID { get; set; }
        public virtual Supplier Supplier { get; set; } = null!;
        public DateTime Date { get; set; } = DateTime.Now;
        
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; } // Positivo para NC, Negativo para Cruces
        
        [StringLength(50)]
        public string? SupportNumber { get; set; } // El número de la Nota Crédito
        
        [StringLength(500)]
        public string? Description { get; set; } // La razón o concepto
    }
}