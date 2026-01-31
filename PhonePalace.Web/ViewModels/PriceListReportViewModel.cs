using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
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

    public class PriceListReportViewModel
    {
        [Display(Name = "Tipo de Lista de Precios")]
        public PriceListType Type { get; set; }
    }
}