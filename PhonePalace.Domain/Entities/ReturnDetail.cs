using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class ReturnDetail
    {
        [Key]
        public int ReturnDetailID { get; set; }
        
        public int ReturnID { get; set; }
        [ForeignKey("ReturnID")]
        public virtual Return Return { get; set; } = null!;
        
        public int ProductID { get; set; }
        [ForeignKey("ProductID")]
        public virtual Product Product { get; set; } = null!;
        
        public int Quantity { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; }
    }
}