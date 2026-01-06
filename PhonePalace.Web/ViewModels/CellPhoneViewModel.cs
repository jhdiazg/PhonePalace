using Microsoft.AspNetCore.Http;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class CellPhoneViewModel
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

        [Required(ErrorMessage = "El modelo es obligatorio.")]
        [Display(Name = "Modelo")]
        public int ModelID { get; set; }

        [Required(ErrorMessage = "La categoría es obligatoria.")]
        [Display(Name = "Categoría")]
        public int CategoryID { get; set; }

        [Required(ErrorMessage = "El precio es obligatorio.")]
        [DataType(DataType.Currency)]
        [Range(100.00, double.MaxValue, ErrorMessage = "El precio debe ser un valor positivo.")]
        [Display(Name = "Precio")]
         [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "El costo es obligatorio.")]
        [DataType(DataType.Currency)]
        [Range(100.00, double.MaxValue, ErrorMessage = "El costo debe ser un valor positivo.")]
        [Display(Name = "Costo")]
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal Cost { get; set; }

        [StringLength(50)]
        [Display(Name = "Color")]
        public string Color { get; set; } = string.Empty;

        [Required(ErrorMessage = "El campo Almacenamiento es obligatorio.")]
        [Display(Name = "Almacenamiento (GB)")]
        public StorageGB StorageGB { get; set; }

        [Required(ErrorMessage = "El campo RAM es obligatorio.")]
        [Display(Name = "RAM (GB)")]
        public RamGB RamGB { get; set; }

        [Required(ErrorMessage = "La condición del producto es obligatoria.")]
        [Display(Name = "Condición del Producto")]
        public ProductCondition ProductCondition { get; set; }

        [Display(Name = "Añadir Nueva Imagen")]
        public IFormFile? NewImageFile { get; set; }

        [Display(Name = "Activo")]
        public bool IsActive { get; set; }

        [Display(Name = "Facturar con IVA")]
        public bool BillWithIVA { get; set; }

        public List<ProductImageViewModel> Images { get; set; } = new List<ProductImageViewModel>();
    }
}