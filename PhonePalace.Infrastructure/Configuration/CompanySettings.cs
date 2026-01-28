namespace PhonePalace.Infrastructure.Configuration
{
    public class CompanySettings
    {
        public string CompanyName { get; set; } = "COMERCIALIZADORA PHONE PALACE SAS";
        public string Email { get; set; } = "ppalace774@gmail.com";
        public string PhoneNumber { get; set; } = "3102808262";
        public string Address { get; set; } = "Cra. 9 # 21-22 Local 102, Bogotá, Colombia";
        public string NIT { get; set; } = "901958644";
        public string WarrantyText { get; set; } = "Para efectos de garantía debe presentar este documento y el producto con todos los accesorios y empaques originales. No se dará garantía por daños ocasionados por alto voltaje, manipulación incorrecta, levantamiento de sellos de seguridad, deterioro, desconfiguración de la máquina o software. Horario garantias 9am a 5pm, Tiempo de respuesta máximo por garantia: 15 dias hábiles. No se responderá por ningun evento que suceda si la comunicación no se hace a través de las lineas de la Empresa sea para servicio o ventas. La Empresa no se hace responsable de ningun proceso que no realice a través de los canales autorizados.";
        public string LogoPath { get; set; } = "wwwroot/images/logo.png";
        public string LogoFacturaPath { get; set; } = "wwwroot/images/logo_fact.png";
    }
}
