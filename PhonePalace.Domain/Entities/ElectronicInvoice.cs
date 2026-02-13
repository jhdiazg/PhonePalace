using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class ElectronicInvoice
    {
        [Key]
        public int Id { get; set; }

        public int InvoiceID { get; set; }
        [ForeignKey("InvoiceID")]
        public virtual Invoice Invoice { get; set; }

        public string CUFE { get; set; }
        public string DianNumber { get; set; } // Ej: SETT-1001
        public string QRCodeUrl { get; set; }
        public string Status { get; set; } // Accepted, Rejected, Pending
        public string? ErrorMessage { get; set; }
        public DateTime IssueDate { get; set; } = DateTime.Now;
    }
}