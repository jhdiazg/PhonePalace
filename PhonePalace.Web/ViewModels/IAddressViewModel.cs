namespace PhonePalace.Web.ViewModels
{
    public interface IAddressViewModel
    {
        string? DepartmentID { get; set; }
        string? MunicipalityID { get; set; }
        string? StreetAddress { get; set; }
    }
}