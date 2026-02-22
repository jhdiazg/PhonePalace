using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhonePalace.Domain.Enums;

namespace PhonePalace.Domain.Entities
{
    public class AccountPayablePayment
    {
        [Key]
        public int Id { get; set; }

        public int AccountPayableId { get; set; }
        [ForeignKey("AccountPayableId")]
        public virtual AccountPayable? AccountPayable { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.Now;

        public PaymentMethod PaymentMethod { get; set; }

        public int? BankId { get; set; }
        [ForeignKey("BankId")]
        public virtual Bank? Bank { get; set; }

        public string? Note { get; set; }
    }
}