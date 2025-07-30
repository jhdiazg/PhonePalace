﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Entities
{
    public class Department
    {
        [Key]
        [StringLength(2)]
        public string DepartmentID { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public virtual ICollection<Municipality> Municipalities { get; set; } = new List<Municipality>();
    }
}