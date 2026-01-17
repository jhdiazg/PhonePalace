using Microsoft.AspNetCore.Mvc.Rendering;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class PurchaseCreateViewModel
    {
        public int SupplierId { get; set; }
        public SelectList? Suppliers { get; set; }
        public SelectList? Categories { get; set; }
        public decimal IVARate { get; set; }
        [Display(Name = "Forma de Pago")]
        public PurchasePaymentMethod PaymentMethod { get; set; }
        [Display(Name = "Productos")]
        [MinLength(1, ErrorMessage = "Debe agregar al menos un producto a la compra.")]
        public List<PurchaseDetailViewModel> Details { get; set; } = new List<PurchaseDetailViewModel>();
    }

    public class PurchaseDetailViewModel
    {
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TaxRate { get; set; } = 0;
    }
}
