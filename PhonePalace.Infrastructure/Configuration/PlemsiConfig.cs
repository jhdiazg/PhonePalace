namespace PhonePalace.Infrastructure.Configuration
{
    public class PlemsiConfig
    {
        // Estos valores por defecto actúan como fallback si no se encuentran en Secrets/Env
        public string BaseUrl { get; set; } = "https://api.plemsi.com/api/";

        // El Token se llenará automáticamente desde User Secrets o Variables de Entorno
        public string Token { get; set; } = string.Empty;

        public string Prefix { get; set; } = "FE";
        public string ResolutionNumber { get; set; } = "18764096962795";

        // Datos de la Resolución (Configurados según correo de Plemsi)
        public int StartRange { get; set; } = 1;
        public int EndRange { get; set; } = 100;
        public string ResolutionDate { get; set; } = "2025-08-13";
        public string ResolutionEndDate { get; set; } = "2027-08-13";

        public bool IsTestEnvironment { get; set; } = false;    }
}
