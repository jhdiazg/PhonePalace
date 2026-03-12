using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels.UserManagement
{
    public class UserEditViewModel
    {
        public string Id { get; set; }

        [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
        [Display(Name = "Nombre de Usuario")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "El formato del correo no es válido.")]
        public string Email { get; set; }

        [Display(Name = "Número de Teléfono")]
        public string? PhoneNumber { get; set; }

        public List<SelectListItem> AllRoles { get; set; } = new List<SelectListItem>();

        [Display(Name = "Roles")]
        public List<string> SelectedRoles { get; set; } = new List<string>();
    }
}