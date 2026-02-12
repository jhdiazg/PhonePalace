using PhonePalace.Domain.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class BankTransaction
    {
        public int BankTransactionID { get; set; }

        [Required]
        public int BankID { get; set; }
        public virtual Bank? Bank { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public BankTransactionType Type { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        [Required]
        public string? Description { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal BalanceAfterTransaction { get; set; }

        // --- Foreign Keys para dar contexto al movimiento ---
        public int? PaymentID { get; set; }
        public virtual Payment? Payment { get; set; }
    }
}