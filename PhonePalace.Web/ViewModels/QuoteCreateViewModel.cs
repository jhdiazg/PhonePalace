using Microsoft.AspNetCore.Mvc.Rendering;
using PhonePalace.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public class QuoteCreateViewModel
    {
        public int QuoteID { get; set; }

        [Required]
        [Display(Name = "Cliente")]
        public int ClientID { get; set; }
        public SelectList? Clients { get; set; }

        [Required]
        [Display(Name = "Fecha de Cotización")]
        [DataType(DataType.Date)]
        public DateTime QuoteDate { get; set; }

        [Required]
        [Display(Name = "Fecha de Expiración")]
        [DataType(DataType.Date)]
        public DateTime ExpirationDate { get; set; }

        public SelectList? Products { get; set; }

        public List<QuoteDetailViewModel> Details { get; set; } = new List<QuoteDetailViewModel>();
    }
}
