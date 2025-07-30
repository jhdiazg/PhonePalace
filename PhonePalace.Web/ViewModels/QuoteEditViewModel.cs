using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class QuoteEditViewModel
    {
        public int QuoteID { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un cliente.")]
        [Display(Name = "Cliente")]
        public int ClientID { get; set; }

        [Required]
        [Display(Name = "Fecha de Cotización")]
        [DataType(DataType.Date)]
        public DateTime QuoteDate { get; set; }

        [Required]
        [Display(Name = "Fecha de Vencimiento")]
        [DataType(DataType.Date)]
        public DateTime ExpirationDate { get; set; }

        [Required]
        [Display(Name = "Estado")]
        public required string Status { get; set; }

        public List<QuoteDetailViewModel> Details { get; set; } = new List<QuoteDetailViewModel>();

        // Propiedades para llenar los dropdowns en la vista
        public SelectList? Clients { get; set; }
        public SelectList? Products { get; set; }
        public SelectList? Statuses { get; set; }
    }
}