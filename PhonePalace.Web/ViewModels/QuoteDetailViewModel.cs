using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class QuoteDetailViewModel
    {
        public int ProductID { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1.")]
        public int Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "El precio unitario debe ser mayor que 0.")]
        public decimal UnitPrice { get; set; }
    }
}
