using System;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class InventoryIndexViewModel
    {
        public int ProductID { get; set; }
        public string? SKU { get; set; }

        [Display(Name = "Producto")]
        public required string ProductName { get; set; }

        [Display(Name = "Costo")]
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal Cost { get; set; }

        [Display(Name = "Precio Venta")]
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal Price { get; set; }

        [Display(Name = "Stock Actual")]
        public int Stock { get; set; }

        [Display(Name = "Valor Inventario")]
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal InventoryValue => Cost * Stock;

        [Display(Name = "Última Actualización")]
        public DateTime? LastUpdated { get; set; }
    }
}