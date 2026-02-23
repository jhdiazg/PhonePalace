namespace PhonePalace.Infrastructure.Configuration
{
    public class CompanySettings
    {
        public string CompanyName { get; set; } = "COMERCIALIZADORA PHONE PALACE SAS";
        public string Email { get; set; } = "ppalace774@gmail.com";
        public string PhoneNumber { get; set; } = "3102808262";
        public string Address { get; set; } = "Cra. 9 # 21-22 Local 102, Bogotá, Colombia";
        public string NIT { get; set; } = "901958644"; // Cambia este valor si tu Token pertenece a otro NIT

        #region DIAN Resolution Settings
        /// <summary>
        /// Número de la resolución de facturación DIAN.
        /// </summary>
        public string DianResolutionNumber { get; set; } = string.Empty;

        /// <summary>
        /// Fecha de expedición de la resolución DIAN (Formato: YYYY-MM-DD).
        /// </summary>
        public string DianResolutionDate { get; set; } = string.Empty;

        /// <summary>
        /// Prefijo de facturación autorizado (ej. "FE").
        /// </summary>
        public string DianResolutionPrefix { get; set; } = string.Empty;

        /// <summary>
        /// Rango inicial de numeración autorizado.
        /// </summary>
        public long DianResolutionStartNumber { get; set; }

        /// <summary>
        /// Rango final de numeración autorizado.
        /// </summary>
        public long DianResolutionEndNumber { get; set; }

        /// <summary>
        /// Fecha de inicio de vigencia de la resolución (Formato: YYYY-MM-DD).
        /// </summary>
        public string DianResolutionStartDate { get; set; } = string.Empty;

        /// <summary>
        /// Fecha de fin de vigencia de la resolución (Formato: YYYY-MM-DD).
        /// </summary>
        public string DianResolutionEndDate { get; set; } = string.Empty;
        #endregion

        public string CreditNoteResolutionNumber { get; set; } = string.Empty;
        public string CreditNotePrefix { get; set; } = string.Empty;

        public string WarrantyText { get; set; } = "Para efectos de garantía debe presentar este documento y el producto con todos los accesorios y empaques originales. No se dará garantía por daños ocasionados por alto voltaje, manipulación incorrecta, levantamiento de sellos de seguridad, deterioro, desconfiguración de la máquina o software. Horario garantias 9am a 5pm, Tiempo de respuesta máximo por garantia: 15 dias hábiles. No se responderá por ningun evento que suceda si la comunicación no se hace a través de las lineas de la Empresa sea para servicio o ventas. La Empresa no se hace responsable de ningun proceso que no realice a través de los canales autorizados.";
        public string LogoPath { get; set; } = "images/logo.png";
        public string LogoFacturaPath { get; set; } = "images/logo_fact.png";
    }
}
