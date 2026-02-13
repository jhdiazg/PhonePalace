namespace PhonePalace.Infrastructure.Configuration
{
    public class PlemsiConfig
    {
        // Estos valores por defecto actúan como fallback si no se encuentran en Secrets/Env
        public string BaseUrl { get; set; } = "https://pruebas.plemsi.com/api/";
        
        // El Token se llenará automáticamente desde User Secrets o Variables de Entorno
        public string Token { get; set; } = string.Empty; 
        
        public string Prefix { get; set; } = "SETT"; 
        public string ResolutionNumber { get; set; } = "18760000001"; 
        
        // Datos de la Resolución (Configurados según correo de Plemsi)
        public int StartRange { get; set; } = 1;
        public int EndRange { get; set; } = 5000000;
        public string ResolutionDate { get; set; } = "2019-01-19";
        public string ResolutionEndDate { get; set; } = "2030-01-19";
        
        public bool IsTestEnvironment { get; set; } = true;
    }
}
