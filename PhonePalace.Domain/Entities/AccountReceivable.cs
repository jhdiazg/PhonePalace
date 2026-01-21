using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class AccountReceivable
    {
        public int AccountReceivableID { get; set; }

        public int ClientID { get; set; }
        public required Client Client { get; set; }

        public DateTime Date { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Balance { get; set; }

        [StringLength(20)]
        public string Type { get; set; } = "Venta"; // "Venta", "Prestamo"

        public int? SaleID { get; set; }
        public Sale? Sale { get; set; }

        public string? Description { get; set; }

        public bool IsPaid { get; set; }

        public ICollection<AccountReceivablePayment> Payments { get; set; } = new List<AccountReceivablePayment>();
    }
}
  