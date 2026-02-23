namespace PhonePalace.Domain.Enums
{
    public enum InventoryMovementType
    {
        Sale,               // Venta
        Purchase,           // Compra (Recepción)
        Return,             // Devolución de Cliente
        Adjustment,         // Ajuste Manual (Inventario Físico)
        SaleCancellation,   // Anulación de Venta
        PurchaseCancellation // Anulación de Compra
    }
}