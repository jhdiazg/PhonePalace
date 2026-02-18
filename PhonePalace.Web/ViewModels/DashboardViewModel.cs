﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class DashboardViewModel
    {
        [Display(Name = "Total de Clientes")]
        public int TotalClients { get; set; }

        [Display(Name = "Total de Productos")]
        public int TotalProducts { get; set; }

        [Display(Name = "Valor del Inventario (Costo)")]
        [DataType(DataType.Currency)]
        public decimal TotalInventoryValue { get; set; }

        [Display(Name = "Ventas del Mes Actual")]
        [DataType(DataType.Currency)]
        public decimal CurrentMonthSales { get; set; }

        [Display(Name = "Saldo en Caja")]
        [DataType(DataType.Currency)]
        public decimal CashBalance { get; set; }

        [Display(Name = "Saldo en Bancos")]
        [DataType(DataType.Currency)]
        public decimal BanksBalance { get; set; }

        [Display(Name = "Cuentas por Cobrar")]
        [DataType(DataType.Currency)]
        public decimal TotalAccountsReceivable { get; set; }

        [Display(Name = "Cuentas por Pagar")]
        [DataType(DataType.Currency)]
        public decimal TotalAccountsPayable { get; set; }

        public List<LowStockProductViewModel> LowStockProducts { get; set; } = new List<LowStockProductViewModel>();
        
        public List<SalesByMarginViewModel> SalesByMargin { get; set; } = new List<SalesByMarginViewModel>();
    }

    public class SalesByMarginViewModel
    {
        public string RangeLabel { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    public class LowStockProductViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int ReorderLevel { get; set; }
    }
}