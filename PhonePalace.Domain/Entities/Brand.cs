using PhonePalace.Domain.Interfaces;
using System;
using System.Collections.Generic;

namespace PhonePalace.Domain.Entities
{
    public class Brand
    {
        public int BrandID { get; set; }
        public string Name { get; set; } = string.Empty; // Ej: "Apple"

        public ICollection<Model> Models { get; set; } = new List<Model>();

        public bool IsActive { get; set; } = true;
    }
}
