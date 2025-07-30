using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace PhonePalace.Web.ViewModels
{
    public class AccessoryViewModel : ProductViewModel
    {
        [Display(Name = "Marca")]
        public int? BrandID { get; set; }

        [Display(Name = "Color")]
        public string? Color { get; set; }
    }
}