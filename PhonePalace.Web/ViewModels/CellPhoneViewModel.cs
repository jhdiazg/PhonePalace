namespace PhonePalace.Web.ViewModels
{
    using Microsoft.AspNetCore.Http;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;

    public class CellPhoneViewModel : ProductViewModel
    {
        [Required(ErrorMessage = "Debe seleccionar un modelo.")]
        [Display(Name = "Modelo")]
        public int ModelID { get; set; }

        [Display(Name = "Color")]
        public string? Color { get; set; } = string.Empty;

        [Display(Name = "Almacenamiento (GB)")]
        public int? StorageGB { get; set; }

        [Display(Name = "RAM (GB)")]
        public int? RamGB { get; set; }
    }
}