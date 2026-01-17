using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Entities
{
    public class PurchaseDetail
    {
        public int Id { get; set; }
        public int PurchaseId { get; set; }
        [DisplayName("Compra")]
        public Purchase? Purchase { get; set; }
        public int ProductId { get; set; }
        [DisplayName("Producto")]
        public Product? Product { get; set; }
        [DisplayName("Cantidad")]
        public int Quantity { get; set; }
        [DisplayName("Recibido")]
        public int ReceivedQuantity { get; set; }
        [DisplayName("Precio Unitario")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal UnitPrice { get; set; }
        [DisplayName("IVA (%)")]
        [Column(TypeName = "decimal(5, 2)")]
        [Range(0, 100)]
        public decimal TaxRate { get; set; } // IVA configurable
        [DisplayName("IVA")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal TaxAmount => Quantity * UnitPrice * TaxRate / 100;
        [DisplayName("Subtotal")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Subtotal => Quantity * UnitPrice;
        [DisplayName("Total")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalPrice => Subtotal + TaxAmount;
    }
}