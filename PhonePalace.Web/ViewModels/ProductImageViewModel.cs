namespace PhonePalace.Web.ViewModels
{
    public class ProductImageViewModel
    {
        public int ProductImageID { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
    }
}
