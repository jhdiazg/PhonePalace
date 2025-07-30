using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Enums
{
    public enum PaymentMethod
    {
        [Display(Name = "Efectivo")]
        Cash,
        [Display(Name = "Tarjeta de Crédito")]
        CreditCard,
        [Display(Name = "Tarjeta de Débito")]
        DebitCard,
        [Display(Name = "Transferencia")]
        Transfer
    }
}