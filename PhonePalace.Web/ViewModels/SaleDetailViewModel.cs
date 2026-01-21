using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class SaleDetailViewModel
    {
        [Required]
        public int ProductID { get; set; }
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1.")]
        public int Quantity { get; set; }

        [Required]
        public decimal UnitPrice { get; set; }

        public string? IMEI { get; set; }
        public string? Serial { get; set; }
    }
}
