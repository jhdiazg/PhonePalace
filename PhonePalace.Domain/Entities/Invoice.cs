using PhonePalace.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class Invoice
    {
        public int InvoiceID { get; set; }

        [Required]
        public int ClientID { get; set; }
        public virtual Client? Client { get; set; }

        [Required]
        public DateTime SaleDate { get; set; }

        [Required]
        public string? UserId { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Tax { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Total { get; set; }

        [Required]
        public InvoiceStatus Status { get; set; }

        public string? CancellationReason { get; set; }
        public DateTime? CancellationDate { get; set; }
        public string? CancelledByUserId { get; set; }
        public DateTime? CompletionDate { get; set; } // New property

        public virtual ICollection<InvoiceDetail> Details { get; set; } = new List<InvoiceDetail>();
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}