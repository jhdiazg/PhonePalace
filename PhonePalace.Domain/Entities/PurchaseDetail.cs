using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

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
        [DisplayName("Precio Unitario")]
        [Column(TypeName = "decimal(18, 2)")]        
        public decimal UnitPrice { get; set; }
        [DisplayName("Total")]
        [Column(TypeName = "decimal(18, 2)")]   
        public decimal TotalPrice => Quantity * UnitPrice;
    }
}