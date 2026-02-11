using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhonePalace.Domain.Enums;

namespace PhonePalace.Domain.Entities
{
    public class Asset
    {
        public int AssetID { get; set; }
        [Required(ErrorMessage = "El nombre del activo es obligatorio.")] 
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        [Required] public DateTime AcquisitionDate { get; set; }
        [Required] 
        [Column(TypeName = "decimal(18, 2)")]
        public decimal AcquisitionCost { get; set; }
        public AssetStatus Status { get; set; }
    }
}