using PhonePalace.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class InventoryReportItemViewModel
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public DateTime LastUpdated { get; set; }
        public int TotalPurchases { get; set; }
        public int TotalSales { get; set; }
    }
}