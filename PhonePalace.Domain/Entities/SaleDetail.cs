using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Entities
{
    public class SaleDetail
    {
    public int SaleDetailID { get; set; }

    public int SaleID { get; set; }
    public required Sale Sale { get; set; }

    public int ProductID { get; set; }
    public required Product Product { get; set; }

    [Required]
    public int Quantity { get; set; }

    [Required]
    [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18, 2)")]
    public decimal UnitPrice { get; set; }

    [StringLength(50)]
    public string? IMEI { get; set; }

    [StringLength(50)]
    public string? Serial { get; set; }
    }
}
