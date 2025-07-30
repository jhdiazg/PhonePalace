using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;

namespace PhonePalace.Web.ViewModels
{
    public class QuoteCreateViewModel
    {
        [Required(ErrorMessage = "Debe seleccionar un cliente.")]
        [Display(Name = "Cliente")]
        public int ClientID { get; set; }

        [Required]
        [Display(Name = "Fecha de Cotización")]
        [DataType(DataType.Date)]
        public DateTime QuoteDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Fecha de Vencimiento")]
        [DataType(DataType.Date)]
        public DateTime ExpirationDate { get; set; } = DateTime.Today.AddDays(15);

        public List<QuoteDetailViewModel> Details { get; set; } = new List<QuoteDetailViewModel>();

        // Propiedades para llenar los dropdowns en la vista
        public SelectList? Clients { get; set; }
        public SelectList? Products { get; set; }
    }

    public class QuoteDetailViewModel
    {
        [Required(ErrorMessage = "Debe seleccionar un producto.")]
        public int ProductID { get; set; }

        [Required, Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1.")]
        public int Quantity { get; set; }

        [Required, Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor a cero.")]
        public decimal UnitPrice { get; set; }
    }
}