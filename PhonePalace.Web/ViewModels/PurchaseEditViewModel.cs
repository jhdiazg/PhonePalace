using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using PhonePalace.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class PurchaseEditViewModel
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public SelectList? Suppliers { get; set; }
        public SelectList? Products { get; set; }
        public decimal IVARate { get; set; }
        [Display(Name = "Forma de Pago")]
        public PurchasePaymentMethod PaymentMethod { get; set; }
        [Display(Name = "No. Factura Proveedor")]
        public string? SupplierInvoiceNumber { get; set; }
        [Display(Name = "Tipo Documento")]
        public AccountPayableDocumentType DocumentType { get; set; }
        [Display(Name = "Observaciones")]
        public string? Observations { get; set; }
        public List<PurchaseDetailViewModel> Details { get; set; } = new List<PurchaseDetailViewModel>();
    }
}
