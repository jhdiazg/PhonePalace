using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Enums
{
    public enum ExpenseType
    {
        [Display(Name = "TRASLADO DE CAJA")]
        CashTransfer = 1,

        [Display(Name = "GASTOS LOCAL")]
        LocalExpenses = 2,

        [Display(Name = "COMISIONES")]
        Commissions = 3
    }
}