using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class ElectronicInvoice
    {
        [Key]
        public int ElectronicInvoiceID { get; set; }

        public int InvoiceID { get; set; }
        [ForeignKey("InvoiceID")]
        public virtual Invoice Invoice { get; set; } = null!;

        public string CUFE { get; set; } = null!;
        public string DianNumber { get; set; } = null!; // Ej: SETT-1001
        public string QRCodeUrl { get; set; } = null!;
        public string Status { get; set; } = "Pending"; // Accepted, Rejected, Pending
        public string? ErrorMessage { get; set; }
        public DateTime IssueDate { get; set; } = DateTime.Now;
    }
}