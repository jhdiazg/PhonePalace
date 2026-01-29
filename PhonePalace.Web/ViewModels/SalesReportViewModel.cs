using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using PhonePalace.Web.Helpers;

namespace PhonePalace.Web.ViewModels
{
    public class SalesReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalSales { get; set; }
        public List<PaymentMethodSummary> Summary { get; set; } = new List<PaymentMethodSummary>();
        public PaginatedList<PaymentDetailDto>? Details { get; set; }
        public string? SelectedPaymentMethod { get; set; }
        public List<SelectListItem> PaymentMethods { get; set; } = new List<SelectListItem>();
    }

    public class PaymentMethodSummary
    {
        public string? PaymentMethod { get; set; }
        public decimal TotalAmount { get; set; }
        public int Count { get; set; }
    }

    public class PaymentDetailDto
    {
        public int PaymentId { get; set; }
        public DateTime Date { get; set; }
        public int InvoiceId { get; set; }
        public int? SaleId { get; set; } // Para enlazar al detalle de la venta
        public string? ClientName { get; set; }
        public required string PaymentMethod { get; set; }
        public decimal Amount { get; set; }
        public string? Reference { get; set; }
    }
}