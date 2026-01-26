using System.Collections.Generic;

namespace PhonePalace.Infrastructure.Configuration
{
    public class PlemsiConfig
    {
        public string ApiKey { get; set; } = string.Empty;
        public Dictionary<string, string> Endpoints { get; set; } = new Dictionary<string, string>();
    }
}