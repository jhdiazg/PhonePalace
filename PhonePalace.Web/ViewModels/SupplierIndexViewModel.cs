using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class SupplierIndexViewModel
    {
        public int SupplierID { get; set; }

        [Display(Name = "Tipo de Proveedor")]
        public string SupplierType { get; set; } = string.Empty;

        [Display(Name = "Nombre / Razón Social")]
        public string DisplayName { get; set; } = string.Empty;

        [Display(Name = "Documento")]
        public string Document { get; set; } = string.Empty;

        [Display(Name = "Correo Electrónico")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Teléfono")]
        public string PhoneNumber { get; set; } = string.Empty;
    }
}
