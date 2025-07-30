using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class InvoiceDetail
    {
        public int InvoiceDetailID { get; set; }
        public int InvoiceID { get; set; }
        public virtual Invoice Invoice { get; set; }

        public int ProductID { get; set; }
        public virtual Product Product { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal UnitPrice { get; set; }

        [NotMapped]
        public decimal LineTotal => Quantity * UnitPrice;
    }
}