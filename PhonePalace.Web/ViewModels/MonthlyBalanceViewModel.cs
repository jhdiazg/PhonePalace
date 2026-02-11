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
        public decimal Sales { get; set; } // Ventas (Subtotal)
        public decimal Cost { get; set; } // Costo de venta
        public decimal Profit { get; set; } // Utilidad
        public decimal FixedExpenses { get; set; } // Gastos Fijos
        public decimal LocalExpenses { get; set; } // Gastos Local (Caja - Fijos)
        public decimal Purchases { get; set; } // Compras (Subtotal)
        public decimal AccountsReceivable { get; set; } // CxC Generadas
        public decimal PurchaseVAT { get; set; } // IVA Compras
        public decimal SalesVAT { get; set; } // IVA Ventas
    }
}