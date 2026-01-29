using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class UserEditViewModel
    {
        public required string UserId { get; set; }
        public required string Email { get; set; }
        
        [Display(Name = "Nombre de Usuario")]
        public required string UserName { get; set; }

        [Display(Name = "Teléfono")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Foto de Perfil")]
        public IFormFile? ProfilePictureFile { get; set; }
        public string? ProfilePictureUrl { get; set; }

        public List<string> SelectedRoles { get; set; } = new List<string>();
        public List<string> AvailableRoles { get; set; } = new List<string>();
    }
}