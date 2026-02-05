using PhonePalace.Domain.Interfaces;
using System;
using System.Collections.Generic;
using PhonePalace.Domain.Enums;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Entities
{
    public class Purchase : ISoftDeletable
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        [DisplayName("Proveedor")]
        public Supplier? Supplier { get; set; }
        [DisplayName("Fecha Orden")]
        public DateTime PurchaseDate { get; set; }
        [DisplayName("Subtotal")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal SubtotalAmount { get; set; }
        [DisplayName("IVA")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal TaxAmount { get; set; }
        [DisplayName("Total")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalAmount { get; set; }
        [DisplayName("Estado")]
        public PurchaseStatus Status { get; set; } // Nuevo campo para el estado de la compra
        [DisplayName("Forma de Pago")]
        public PurchasePaymentMethod PaymentMethod { get; set; }
        [DisplayName("No. Factura Proveedor")]
        public string? SupplierInvoiceNumber { get; set; }
        [DisplayName("Tipo Documento")]
        public AccountPayableDocumentType DocumentType { get; set; } = AccountPayableDocumentType.Invoice;
        [DisplayName("Observaciones")]
        [StringLength(500)]
        public string? Observations { get; set; }
        public ICollection<PurchaseDetail> PurchaseDetails { get; set; } = new List<PurchaseDetail>();
        public bool IsDeleted { get; set; }
        public DateTime? DeletedDate { get; set; }
        public DateTime? DeletedOn { get; set; }
    }
}