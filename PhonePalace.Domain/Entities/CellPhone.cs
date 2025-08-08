using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhonePalace.Domain.Enums;

namespace PhonePalace.Domain.Entities
{
    /// <summary>
    /// Representa un producto de tipo Teléfono Celular.
    /// </summary>
    public class CellPhone : Product
    {
        public int ModelID { get; set; }
        [DisplayName("Modelo")]
        public virtual Model Model { get; set; } = null!; // El modelo ya contiene la marca (Brand)
        public string? Color { get; set; } = string.Empty;
        [DisplayName("Almacenamiento (GB)")]
        public StorageGB StorageGB { get; set; }

        [Display(Name = "RAM (GB)")]
        public RamGB RamGB { get; set; }
    }
}