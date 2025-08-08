using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Domain.Enums
{
    public enum PurchaseStatus
    {
        [Display(Name = "Borrador")]
        Draft, // Borrador: La compra ha sido iniciada pero no confirmada.
        [Display(Name = "Ordenada")]
        Ordered, // Ordenada: La compra ha sido confirmada y enviada al proveedor.
        [Display(Name = "Parcialmente Recibida")]
        PartiallyReceived, // Parcialmente Recibida: Parte de la mercancía ha llegado.
        [Display(Name = "Recibida")]
        Received, // Recibida: Toda la mercancía ha llegado.
        [Display(Name = "Facturada")]
        Billed, // Facturada: El proveedor ha enviado la factura.
        [Display(Name = "Pagada")]
        Paid, // Pagada: La compra ha sido completamente pagada.
        [Display(Name = "Cancelada")]
        Cancelled // Cancelada: La compra ha sido anulada.
    }
}