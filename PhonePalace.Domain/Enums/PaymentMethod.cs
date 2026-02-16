﻿using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Enums
{
    public enum PaymentMethod
    {
        [Display(Name = "Efectivo")]
        Cash,
        [Display(Name = "Tarjeta Crédito")]
        CreditCard,
        [Display(Name = "Tarjeta Débito")]
        DebitCard,
        [Display(Name = "Transferencia")]
        Transfer,
        [Display(Name = "Crédito")]
        Credit,
        [Display(Name = "Saldo a Favor")]
        CustomerBalance
    }
}