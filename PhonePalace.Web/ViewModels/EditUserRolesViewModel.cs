using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class EditUserRolesViewModel
    {
        public string UserId { get; set; } = string.Empty;

        [Display(Name = "Usuario")]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Roles Disponibles")]
        public List<string> AvailableRoles { get; set; } = new List<string>();

        [Display(Name = "Roles Asignados")]
        public List<string> SelectedRoles { get; set; } = new List<string>();
    }
}