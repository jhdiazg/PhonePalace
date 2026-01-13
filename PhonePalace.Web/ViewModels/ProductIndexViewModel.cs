﻿using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class ProductIndexViewModel
    {
        [Display(Name = "Activo")]
        public bool IsActive { get; set; }
        public int ProductID { get; set; }
        [Display(Name = "SKU")]
        public string? SKU { get; set; }
        [Display(Name = "Código")]
        public string? Code { get; set; }
        [Display(Name = "Nombre")]
        public required string Name { get; set; }
        [Display(Name = "Tipo")]
        public required string ProductType { get; set; } // "Celular" o "Accesorio"
        [Display(Name = "Categoría")]
        public required string CategoryName { get; set; }
        [Display(Name = "Marca")]
        public string? BrandName { get; set; }
        [Display(Name = "Modelo")]
        public string? ModelName { get; set; }
        [Display(Name = "Precio")]
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal Price { get; set; }
        [Display(Name = "Costo")]
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal Cost { get; set; }
        [Display(Name = "Imagen")]
        public string? PrimaryImageUrl { get; set; }
    }
}