namespace PhonePalace.Web.ViewModels
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        public int? StatusCode { get; set; }

        public bool IsNotFound => StatusCode == 404;
    }
}