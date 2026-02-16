using System;
using System.Collections.Generic;

namespace PhonePalace.Web.ViewModels
{
    public class ReturnCreateViewModel
    {
        public int SaleID { get; set; }
        public string? ClientName { get; set; }
        public DateTime SaleDate { get; set; }
        public List<ReturnDetailViewModel> Details { get; set; } = new List<ReturnDetailViewModel>();
    }

    public class ReturnDetailViewModel
    {
        public int ProductID { get; set; }
        public string? ProductName { get; set; }
        public int SoldQuantity { get; set; }
        public int PreviouslyReturned { get; set; }
        public decimal UnitPrice { get; set; }
        public int QuantityToReturn { get; set; }
    }
}