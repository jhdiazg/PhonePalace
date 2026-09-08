namespace PhonePalace.Web.ViewModels
{
    public class SalesIndexRowViewModel
    {
        public int ID { get; set; }
        public string RowType { get; set; } = "Sale";
        public DateTime Date { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ReferenceLabel { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal Profit { get; set; }
        public bool IsCreditPending { get; set; }
    }
}
