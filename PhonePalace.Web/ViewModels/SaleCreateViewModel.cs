using Microsoft.AspNetCore.Mvc.Rendering;
using PhonePalace.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class SaleCreateViewModel
    {
        [Required(ErrorMessage = "Debe seleccionar un cliente.")]
        [Display(Name = "Cliente")]
        public int ClientID { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de Venta")]
        public DateTime SaleDate { get; set; } = DateTime.Today;

        public List<InvoiceDetailViewModel> Details { get; set; } = new();
        public List<PaymentViewModel> Payments { get; set; } = new();

        // Para poblar los dropdowns
        public SelectList? Clients { get; set; }
        public SelectList? Products { get; set; }
        public SelectList? PaymentMethods { get; set; }
    }

    public class PaymentViewModel
    {
        [Required(ErrorMessage = "El método de pago es obligatorio.")]
        [Display(Name = "Método de Pago")]
        public PaymentMethod PaymentMethod { get; set; }

        [Required(ErrorMessage = "El monto es obligatorio.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a cero.")]
        [Display(Name = "Monto")]
        public decimal Amount { get; set; }

        [Display(Name = "Referencia")]
        [StringLength(100)]
        public string? ReferenceNumber { get; set; }
    }
    
    public class InvoiceDetailViewModel
    {
        [Required] public int ProductID { get; set; }
        [Required] [Range(1, int.MaxValue)] public int Quantity { get; set; }
        [Required] [Range(0, double.MaxValue)] public decimal UnitPrice { get; set; }
    }
}