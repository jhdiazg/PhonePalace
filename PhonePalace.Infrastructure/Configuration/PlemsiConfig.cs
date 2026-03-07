namespace PhonePalace.Infrastructure.Configuration
{
    public class PlemsiConfig
    {
        // Se elimina BaseUrl para forzar una configuración explícita.
        public string TestUrl { get; set; } = "https://pruebas.plemsi.com/api/";
        public string ProductionUrl { get; set; } = "https://api.plemsi.com/api/"; // URL de producción de Plemsi

        // El Token se llenará automáticamente desde User Secrets o Variables de Entorno
        public string Token { get; set; } = string.Empty;

        // Este valor se puede seguir usando si es necesario para otras lógicas.
        public bool IsTestEnvironment { get; set; } = true;
    }
}
