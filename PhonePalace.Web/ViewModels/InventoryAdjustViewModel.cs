namespace PhonePalace.Web.ViewModels
{
    public class InventoryAdjustViewModel
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
    }
}