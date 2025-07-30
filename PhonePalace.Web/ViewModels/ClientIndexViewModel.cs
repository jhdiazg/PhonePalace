using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class ClientIndexViewModel
    {
        public int ClientID { get; set; }
        [Display(Name = "Tipo de Cliente")]
        public string ClientType { get; set; } = string.Empty;
        [Display(Name = "Nombre / Razón Social")]
        public string DisplayName { get; set; } = string.Empty;
        [Display(Name = "Documento / NIT")]
        public string Document { get; set; } = string.Empty;
        [Display(Name = "Correo Electrónico")]
        public string? Email { get; set; }
        [Display(Name = "Teléfono")]
        public string? PhoneNumber { get; set; }
    }
}