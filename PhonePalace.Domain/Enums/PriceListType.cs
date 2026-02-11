using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Enums
{
    public enum PriceListType
    {
        [Display(Name = "Almacén (35%)")]
        Almacen = 35,
        [Display(Name = "Instalador (60%)")]
        Instalador = 60,
        [Display(Name = "Local (85%)")]
        Local = 85
    }
}