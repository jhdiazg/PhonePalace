using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhonePalace.Domain.Enums;
using System;

namespace PhonePalace.Web.ViewModels
{
    public class SaleCreateViewModel
    {
        public int? ClientID { get; set; }
        [Display(Name = "Fecha de Venta")]
        public DateTime SaleDate { get; set; } = DateTime.Now;
        [Display(Name = "Canal de Venta")]
        public SaleChannel? SaleChannel { get; set; }

        public List<SaleDetailViewModel> Details { get; set; } = new List<SaleDetailViewModel>();
        public List<PaymentViewModel> Payments { get; set; } = new List<PaymentViewModel>();

        // For dropdowns (no deben ser validados en el POST)
        [NotMapped]
        public SelectList? Clients { get; set; }
        [NotMapped]
        public SelectList? Products { get; set; }
        [NotMapped]
        public SelectList? PaymentMethods { get; set; }
        [NotMapped]
        public SelectList? SaleChannels { get; set; }
    }
}
