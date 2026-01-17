using PhonePalace.Domain.Enums;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class PurchaseReceiveViewModel
    {
        public int PurchaseId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        
        [Display(Name = "No. Factura Proveedor")]
        public string? SupplierInvoiceNumber { get; set; }
        [Display(Name = "Tipo Documento")]
        public AccountPayableDocumentType DocumentType { get; set; }
        public List<PurchaseReceiveDetailViewModel> Details { get; set; } = new List<PurchaseReceiveDetailViewModel>();
    }

    public class PurchaseReceiveDetailViewModel
    {
        public int PurchaseDetailId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int OrderedQuantity { get; set; }
        public int PreviouslyReceivedQuantity { get; set; }
        public int QuantityToReceive { get; set; }
        public decimal UnitPrice { get; set; }
    }
}