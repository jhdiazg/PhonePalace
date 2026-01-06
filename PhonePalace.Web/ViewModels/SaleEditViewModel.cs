using Microsoft.AspNetCore.Mvc.Rendering;
using PhonePalace.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class SaleEditViewModel
    {
        public int SaleID { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un cliente.")]
        [Display(Name = "Cliente")]
        public int ClientID { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de Venta")]
        public DateTime SaleDate { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un canal de venta.")]
        [Display(Name = "Canal de Venta")]
        public SaleChannel SaleChannel { get; set; }

        public List<InvoiceDetailViewModel> Details { get; set; } = new();

        // Para poblar los dropdowns
        public SelectList? Clients { get; set; }
        public SelectList? Products { get; set; }
        public SelectList? SaleChannels { get; set; }
    }
}
