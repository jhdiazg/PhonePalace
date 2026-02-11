using System.ComponentModel.DataAnnotations;
using PhonePalace.Domain.Enums;

namespace PhonePalace.Web.ViewModels
{
    public class PriceListReportViewModel
    {
        [Display(Name = "Tipo de Lista de Precios")]
        public PriceListType Type { get; set; }
    }
}