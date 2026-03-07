using System;
using System.Collections.Generic;
using PhonePalace.Domain.Enums;

namespace PhonePalace.Domain.DTOs
{
    public class SaleCreateDto
    {
        public int? ClientID { get; set; }
        public DateTime SaleDate { get; set; }
        public SaleChannel? SaleChannel { get; set; }
        public List<SaleDetailDto> Details { get; set; } = new();
        public List<PaymentDto> Payments { get; set; } = new();
    }

    public class SaleDetailDto
    {
        public int ProductID { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string? IMEI { get; set; }
        public string? Serial { get; set; }
    }

    public class PaymentDto
    {
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? ReferenceNumber { get; set; }
        public int? BankID { get; set; }
        public int PaymentID { get; set; }
    }
}