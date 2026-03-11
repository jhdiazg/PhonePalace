using System;

namespace PhonePalace.Web.ViewModels
{
    public class AccountPayableIndexViewModel
    {
        public int Id { get; set; }
        public string DocumentType { get; set; } = string.Empty; // Contendrá el nombre en español
        public string DocumentNumber { get; set; } = string.Empty;
        public string Beneficiary { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Balance { get; set; }
        public DateTime DueDate { get; set; }
        public bool IsPaid { get; set; }
        public int? PurchaseId { get; set; }
        public string Type { get; set; } = string.Empty;
    }
}