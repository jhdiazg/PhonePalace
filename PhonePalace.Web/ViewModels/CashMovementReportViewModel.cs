using PhonePalace.Domain.Entities;
using System;
using System.Collections.Generic;

namespace PhonePalace.Web.ViewModels
{
    public class CashMovementReportViewModel
    {
        public DateTime ReportDate { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal ExpectedBalance { get; set; }
        public List<CashMovement> Movements { get; set; } = new List<CashMovement>();
        public bool IsCashRegisterFound { get; set; }
    }
}
