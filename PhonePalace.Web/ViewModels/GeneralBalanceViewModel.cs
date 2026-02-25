using System;

namespace PhonePalace.Web.ViewModels
{
    public class GeneralBalanceViewModel
    {
        public DateTime ReportDate { get; set; }

        // Activos
        public decimal InventoryValue { get; set; }
        public decimal AccountsReceivable { get; set; }
        public decimal Cash { get; set; }
        public decimal Banks { get; set; } // Bancos tradicionales
        public decimal Nequi { get; set; }
        public decimal Daviplata { get; set; }
        public decimal FixedAssets { get; set; }
        
        public decimal TotalAssets => InventoryValue + AccountsReceivable + Cash + Banks + Nequi + Daviplata + FixedAssets;

        // Pasivos
        public decimal AccountsPayable { get; set; }
        public decimal Credits { get; set; } // Otros créditos u obligaciones

        public decimal TotalLiabilities => AccountsPayable + Credits;

        // Patrimonio / Resultado
        public decimal NetResult => TotalAssets - TotalLiabilities;
    }
}