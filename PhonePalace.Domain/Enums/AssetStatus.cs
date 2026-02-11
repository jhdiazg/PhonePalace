using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Enums
{
    public enum AssetStatus
    {
        [Display(Name = "Activo")]
        Active,
        [Display(Name = "Vendido")]
        Sold,
        [Display(Name = "Dado de Baja")]
        Decommissioned
    }
}