using PhonePalace.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System;

namespace PhonePalace.Domain.Entities
{
    public class CreditCardVerification
    {
        [Key]
        public int CreditCardVerificationID { get; set; }

        public int SaleID { get; set; }
        public virtual Sale Sale { get; set; }

        public int PaymentID { get; set; }
        public virtual Payment Payment { get; set; }

        public int BankID { get; set; }
        public virtual Bank Bank { get; set; }

        public decimal Amount { get; set; }

        public DateTime CreationDate { get; set; }

        public DateTime? VerificationDate { get; set; }

        public VerificationStatus Status { get; set; }

        public string? VerificationNotes { get; set; }
    }
}
