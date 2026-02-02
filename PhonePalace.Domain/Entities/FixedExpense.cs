using PhonePalace.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class FixedExpense
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "El concepto es obligatorio.")]
        [StringLength(150)]
        [Display(Name = "Concepto")]
        public string Concept { get; set; } = string.Empty;

        [Required(ErrorMessage = "El monto es obligatorio.")]
        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "Monto")]
        public decimal Amount { get; set; }

        [Display(Name = "Activo")]
        public bool IsActive { get; set; } = true;
    }
}