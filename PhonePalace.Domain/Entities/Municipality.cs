﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class Municipality
    {
        [Key]
        [StringLength(5)]
        public string MunicipalityID { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(2)]
        public string DepartmentID { get; set; } = string.Empty;

        [ForeignKey("DepartmentID")]
        public virtual Department Department { get; set; } = null!;
    }
}