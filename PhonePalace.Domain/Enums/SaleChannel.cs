
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Enums
{
    public enum SaleChannel
    {
        [Display(Name = "-- Seleccione un Canal de Venta --")]
        None = 0,
        [Display(Name = "Tienda física")]
        InStore = 1,

        [Display(Name = "En línea")]
        Online = 2,

        [Display(Name = "Redes sociales")]
        SocialMedia = 3,

        [Display(Name = "Teléfono")]
        Phone = 4,

        [Display(Name = "Cotización")]
        Quotations = 5,

        [Display(Name = "Otro")]
        Other = 6
    }
}

