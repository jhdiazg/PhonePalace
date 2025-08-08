using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Enums
{
    public enum RamGB : int
    {
        [Display(Name = "2 GB")]
        _2 = 2,
        [Display(Name = "4 GB")]
        _4 = 4,
        [Display(Name = "6 GB")]
        _6 = 6,
        [Display(Name = "8 GB")]
        _8 = 8,
        [Display(Name = "12 GB")]
        _12 = 12,
        [Display(Name = "16 GB")]
        _16 = 16
    }
}