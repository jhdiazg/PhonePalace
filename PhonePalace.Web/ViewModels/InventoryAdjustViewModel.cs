using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class InventoryAdjustViewModel
    {
        public int ProductID { get; set; }

        [Display(Name = "Producto")]
        public required string ProductName { get; set; }

        [Display(Name = "Stock Actual")]
        public int CurrentStock { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "La nueva cantidad no puede ser negativa.")]
        [Display(Name = "Nueva Cantidad")]
        public int NewStock { get; set; }

        [Required(ErrorMessage = "El motivo del ajuste es obligatorio.")]
        [StringLength(250, ErrorMessage = "El motivo no puede exceder los 250 caracteres.")]
        [Display(Name = "Motivo del Ajuste")]
        public string Reason { get; set; } = string.Empty;
    }
}