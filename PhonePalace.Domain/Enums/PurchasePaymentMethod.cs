using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Enums
{
    public enum PurchasePaymentMethod
    {
        [Display(Name = "Contado")]
        Cash,
        [Display(Name = "Crédito")]
        Credit,
        [Display(Name = "Transferencia")]
        Transfer   

    }
}