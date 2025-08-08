using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Enums
{
    public enum StorageGB : int
    {
        [Display(Name = "16 GB")]
        _16 = 16,
        [Display(Name = "32 GB")]
        _32 = 32,
        [Display(Name = "64 GB")]
        _64 = 64,
        [Display(Name = "128 GB")]
        _128 = 128,
        [Display(Name = "256 GB")]
        _256 = 256,
        [Display(Name = "512 GB")]
        _512 = 512,
        [Display(Name = "1 TB")]
        _1024 = 1024
    }
}