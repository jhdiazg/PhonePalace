using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PhonePalace.Domain.Enums;

namespace PhonePalace.Domain.Entities
{
    public class Sale
    {
    public int SaleID { get; set; }

    public int ClientID { get; set; }
    public required Client Client { get; set; }

    public int? InvoiceID { get; set; }
    public required Invoice Invoice { get; set; }

    [Required]
    [Display(Name = "Fecha de Venta")]
    public DateTime SaleDate { get; set; }

    [Required]
    [Display(Name = "Total Venta")]
    [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18, 2)")]
    public decimal TotalAmount { get; set; }

    public bool IsDeleted { get; set; } = false;

    public required ICollection<SaleDetail> Details { get; set; }
    }
}
