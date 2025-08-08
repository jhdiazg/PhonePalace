using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public abstract class ProductViewModel
    {
        public int ProductID { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100)]
        [Display(Name = "Nombre del Producto")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Descripción")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "El precio es obligatorio.")]
        [Range(100.00, double.MaxValue, ErrorMessage = "El precio debe ser mayor que cero.")]
        [Display(Name = "Precio de Venta")]
        [DisplayFormat(DataFormatString = "{0:c}", ApplyFormatInEditMode = true)]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "El costo es obligatorio.")]
        [Range(100.00, double.MaxValue, ErrorMessage = "El costo debe ser mayor que cero.")]
        [Display(Name = "Costo")]
        [DisplayFormat(DataFormatString = "{0:c}", ApplyFormatInEditMode = true)]
        public decimal Cost { get; set; }

        [Required(ErrorMessage = "El SKU es obligatorio.")]
        public string SKU { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe seleccionar una categoría.")]
        [Display(Name = "Categoría")]
        public int CategoryID { get; set; }

        [Display(Name = "Activo")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Añadir Imagen")]
        public IFormFile? NewImageFile { get; set; }
        public List<ProductImageViewModel> Images { get; set; } = new List<ProductImageViewModel>();
    }
}