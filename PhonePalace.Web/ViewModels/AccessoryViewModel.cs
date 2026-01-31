using Microsoft.AspNetCore.Http;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class AccessoryViewModel
    {
        public int ProductID { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100)]
        [Display(Name = "Nombre")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "El SKU es obligatorio.")]
        [StringLength(50)]
        public string SKU { get; set; } = string.Empty;

        [Display(Name = "Descripción")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "La categoría es obligatoria.")]
        [Display(Name = "Categoría")]
        public int CategoryID { get; set; }

        [Display(Name = "Marca")]
        public int? BrandID { get; set; }

        [Required(ErrorMessage = "El precio es obligatorio.")]
        [DataType(DataType.Currency)]
        [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser un valor positivo.")]
        [Display(Name = "Precio")]
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "El costo es obligatorio.")]
        [DataType(DataType.Currency)]
        [Range(0.01, double.MaxValue, ErrorMessage = "El costo debe ser un valor positivo.")]
        [Display(Name = "Costo")]
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal Cost { get; set; }

        [StringLength(50)]
        [Display(Name = "Color")]
        public string? Color { get; set; }

        [Required(ErrorMessage = "La condición del producto es obligatoria.")]
        [Display(Name = "Condición del Producto")]
        public ProductCondition ProductCondition { get; set; }

        [StringLength(100)]
        [Display(Name = "Material")]
        public string? Material { get; set; }

        [StringLength(255)]
        [Display(Name = "Compatibilidad")]
        public string? Compatibility { get; set; }

        [Display(Name = "Añadir Nueva Imagen")]
        public IFormFile? NewImageFile { get; set; }

        [Display(Name = "Activo")]
        public bool IsActive { get; set; } = true;



        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    }
}