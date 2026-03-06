namespace PhonePalace.Infrastructure.Configuration
{
    public class PlemsiConfig
    {
        // Estos valores por defecto actúan como fallback si no se encuentran en Secrets/Env
        public string BaseUrl { get; set; } = "https://pruebas.plemsi.com/api/";

        // El Token se llenará automáticamente desde User Secrets o Variables de Entorno
        public string Token { get; set; } = string.Empty;

        public bool IsTestEnvironment { get; set; } = false;
    }
}
