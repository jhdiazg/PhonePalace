using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PhonePalace.Web.ViewModels
{
    public class CreateUserViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Correo Electrónico")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirmar Contraseña")]
        [Compare("Password", ErrorMessage = "Las contraseñas no coinciden.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Roles Asignados")]
        public List<string> SelectedRoles { get; set; } = new List<string>();

        [Display(Name = "Roles Disponibles")]
        public List<string> AvailableRoles { get; set; } = new List<string>();
        
        [Display(Name = "Foto de Perfil")]
        public IFormFile? ProfilePictureFile { get; set; }
    }
}