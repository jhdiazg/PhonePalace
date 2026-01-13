using PhonePalace.Domain.Entities;
using System.Collections.Generic;

namespace PhonePalace.Web.ViewModels
{
    public class CashIndexViewModel
    {
        public CashRegister? CurrentCashRegister { get; set; }
        public decimal CurrentBalance { get; set; }
        public IEnumerable<CashMovement>? RecentMovements { get; set; }
    }
}