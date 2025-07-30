using PhonePalace.Domain.Interfaces;
using System;
using System.Collections.Generic;

namespace PhonePalace.Domain.Entities
{
    public class Model
    {
        public int ModelID { get; set; }
        public string Name { get; set; } = string.Empty; // Ej: "iPhone 14 Pro Max"
        
        public int BrandID { get; set; }
        public virtual Brand Brand { get; set; } = null!;

        public virtual ICollection<CellPhone> CellPhones { get; set; } = new List<CellPhone>();

        public bool IsActive { get; set; } = true;
    }
}
