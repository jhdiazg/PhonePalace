using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhonePalace.Domain.Enums;

namespace PhonePalace.Domain.Entities
{
    public class CreditCardVerification
    {
        public int CreditCardVerificationID { get; set; }

        public int? SaleID { get; set; }
        public Sale? Sale { get; set; }

        public int? PaymentID { get; set; } // Ahora es opcional
        public Payment? Payment { get; set; }

        public int? AccountReceivablePaymentID { get; set; } // Nueva relación para abonos de CxC
        public AccountReceivablePayment? AccountReceivablePayment { get; set; }

        public int BankID { get; set; }
        public Bank Bank { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        public DateTime CreationDate { get; set; }

        public VerificationStatus Status { get; set; }

        public DateTime? VerificationDate { get; set; }
        public string? VerificationNotes { get; set; }
    }
}