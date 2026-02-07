using System;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Domain.Enums;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Entities
{
    public class AccountPayable : ISoftDeletable
    {
        public int Id { get; set; }
        public int? PurchaseId { get; set; }
        [DisplayName("Compra")]
        public Purchase? Purchase { get; set; }
        [DisplayName("Valor")]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }
        [DisplayName("Fecha de vencimiento")]
        public DateTime DueDate { get; set; }
        [DisplayName("Pagada")]
        public bool IsPaid { get; set; }

        [DisplayName("Tipo de documento")]
        public AccountPayableDocumentType DocumentType { get; set; }

        [DisplayName("Número de documento")]
        public string? DocumentNumber { get; set; } // New property

        [DisplayName("Beneficiario / Proveedor")]
        [StringLength(100)]
        public string? Beneficiary { get; set; }

        public DateTime CreatedDate { get; set; }
        public DateTime? DeletedDate { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedOn { get; set; }
    }
}