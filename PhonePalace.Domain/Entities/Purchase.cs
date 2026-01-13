using PhonePalace.Domain.Interfaces;
using System;
using System.Collections.Generic;
using PhonePalace.Domain.Enums;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

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
        public decimal SubtotalAmount { get; set; }
        [DisplayName("IVA")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal TaxAmount { get; set; }
        [DisplayName("Total")]
        public decimal TotalAmount { get; set; }
        [DisplayName("Estado")]
        public PurchaseStatus Status { get; set; } // Nuevo campo para el estado de la compra
    public ICollection<PurchaseDetail> PurchaseDetails { get; set; } = new List<PurchaseDetail>();
        public bool IsDeleted { get; set; }
        public DateTime? DeletedDate { get; set; }
        public DateTime? DeletedOn { get; set; }
    }
}