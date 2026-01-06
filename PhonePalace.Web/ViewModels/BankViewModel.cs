using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class BankViewModel
    {
        public int BankID { get; set; }

        [Required(ErrorMessage = "El nombre del banco es obligatorio.")]
        [StringLength(100)]
        [Display(Name = "Nombre del Banco")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Activo")]
        public bool IsActive { get; set; } = true;
    }
}
