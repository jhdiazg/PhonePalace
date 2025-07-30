using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class Quote
    {
        public int QuoteID { get; set; }

        [Required]
        public int ClientID { get; set; }
        public required Client Client { get; set; }

        [Required]
        public DateTime QuoteDate { get; set; }

        [Required]
        public DateTime ExpirationDate { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Tax { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Total { get; set; }

        [Required]
        [StringLength(50)]
        public string? Status { get; set; } // e.g., "Pending", "Approved", "Expired"

        public ICollection<QuoteDetail> Details { get; set; } = new List<QuoteDetail>();
    }
}
