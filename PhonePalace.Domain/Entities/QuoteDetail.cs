using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class QuoteDetail
    {
        public int QuoteDetailID { get; set; }
        public int QuoteID { get; set; }
        public required Quote Quote { get; set; }

        public int ProductID { get; set; }
        public required Product Product { get; set; } // Asumo que tienes una entidad Product

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal UnitPrice { get; set; }

        [NotMapped]
        public decimal LineTotal => Quantity * UnitPrice;
    }
}
