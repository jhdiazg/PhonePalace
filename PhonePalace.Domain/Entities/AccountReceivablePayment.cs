using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class AccountReceivablePayment
    {
        public int AccountReceivablePaymentID { get; set; }

        public int AccountReceivableID { get; set; }
        public required AccountReceivable AccountReceivable { get; set; }

        public DateTime Date { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        public string? Note { get; set; }
    }
}
