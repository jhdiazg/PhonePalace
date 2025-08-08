using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace PhonePalace.Web.ViewModels
{
    public class PurchaseEditViewModel
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public SelectList? Suppliers { get; set; }
        public SelectList? Products { get; set; }
        public List<PurchaseDetailViewModel> Details { get; set; } = new List<PurchaseDetailViewModel>();
    }
}
