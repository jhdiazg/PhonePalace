using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Entities
{
    public class ProductImage
    {
        public int ProductImageID { get; set; }

        public int ProductID { get; set; }
        public virtual Product Product { get; set; } = null!;

        [Required]
        public string ImageUrl { get; set; } = string.Empty;

        public bool IsPrimary { get; set; }
    }
}


