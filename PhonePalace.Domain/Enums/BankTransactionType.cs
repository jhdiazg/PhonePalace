using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Enums
{
    public enum BankTransactionType
    {
        [Display(Name = "Ingreso por Venta")]
        SaleIncome,

        [Display(Name = "Ingreso (Transferencia)")]
        TransferIn,

        [Display(Name = "Egreso (Transferencia)")]
        TransferOut,

        [Display(Name = "Ajuste de Ingreso")]
        ManualIncome,

        [Display(Name = "Ajuste de Egreso")]
        ManualExpense
    }
}