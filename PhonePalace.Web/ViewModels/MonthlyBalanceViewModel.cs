using System.Collections.Generic;

namespace PhonePalace.Web.ViewModels
{
    public class MonthlyBalanceViewModel
    {
        public int Year { get; set; }
        public List<MonthlyBalanceItem> Items { get; set; } = new List<MonthlyBalanceItem>();
        public MonthlyBalanceItem Totals { get; set; } = new MonthlyBalanceItem();
    }

    public class MonthlyBalanceItem
    {
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal Sales { get; set; }
        public decimal SalesVAT { get; set; }
        public decimal Cost { get; set; }
        public decimal FixedExpenses { get; set; }
        public decimal LocalExpenses { get; set; }
        public decimal Purchases { get; set; }
        public decimal PurchaseVAT { get; set; }
        public decimal AccountsReceivable { get; set; }
        public decimal AssetsValue { get; set; }
        public decimal OtherIncome { get; set; }
        public decimal Profit { get; set; }
    }
}