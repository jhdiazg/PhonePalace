using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class ModelViewModel
    {
        public int ModelID { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100)]
        [Display(Name = "Nombre del Modelo")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe seleccionar una marca.")]
        [Display(Name = "Marca")]
        public int BrandID { get; set; }

        public string? BrandName { get; set; }
        public bool IsActive { get; set; } = true;
    }
}