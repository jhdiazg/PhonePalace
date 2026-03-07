using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhonePalace.Domain.DTOs;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;

namespace PhonePalace.Infrastructure.Services
{
    public class SalesResult : ISalesResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int? InvoiceId { get; set; }

        public static SalesResult Fail(string message) => new SalesResult { Success = false, ErrorMessage = message };
        public static SalesResult Ok(int invoiceId) => new SalesResult { Success = true, InvoiceId = invoiceId };
    }

    public class SalesService : ISalesService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICashService _cashService;
        private readonly IBankService _bankService;
        private readonly IConfiguration _config;
        private readonly IAuditService _auditService;

        public SalesService(ApplicationDbContext context, ICashService cashService, IBankService bankService, IConfiguration config, IAuditService auditService)
        {
            _context = context;
            _cashService = cashService;
            _bankService = bankService;
            _config = config;
            _auditService = auditService;
        }

        public async Task<ISalesResult> ProcessSaleAsync(SaleCreateDto dto, string userId)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var client = await _context.Clients.FindAsync(dto.ClientID!.Value);
                    if (client == null) return SalesResult.Fail("Cliente no encontrado.");

                    // --- OPTIMIZACIÓN: Obtener todos los productos e inventarios de una sola vez ---
                    var productIds = dto.Details.Select(d => d.ProductID).ToList();
                    var productsFromDb = await _context.Products
                        .Where(p => productIds.Contains(p.ProductID))
                        .ToDictionaryAsync(p => p.ProductID);
                    
                    var inventoriesFromDb = await _context.Inventories
                        .Where(i => productIds.Contains(i.ProductID))
                        .ToDictionaryAsync(i => i.ProductID);

                    // 1. Validar Stock y Precios
                    foreach (var detailVM in dto.Details)
                    {
                        if (!productsFromDb.TryGetValue(detailVM.ProductID, out var product))
                            return SalesResult.Fail($"Producto con ID '{detailVM.ProductID}' no encontrado.");

                        if (!inventoriesFromDb.TryGetValue(detailVM.ProductID, out var inventory) || inventory.Stock < detailVM.Quantity)
                            return SalesResult.Fail($"Stock insuficiente para '{product.Name}'. Disponible: {inventoriesFromDb.GetValueOrDefault(detailVM.ProductID)?.Stock ?? 0}");

                        if (detailVM.UnitPrice < product.Cost)
                            return SalesResult.Fail($"El precio de '{product.Name}' es inferior al costo.");
                    }

                    // 2. Calcular Totales
                    decimal total = dto.Details.Sum(d => d.Quantity * d.UnitPrice);

                    // 3. Recargo Tarjetas
                    decimal surchargePercentage = _config.GetValue<decimal>("SalesSettings:CardSurchargePercentage");
                    if (dto.Payments != null && surchargePercentage > 0)
                    {
                        foreach (var p in dto.Payments)
                        {
                            if (Enum.TryParse<PaymentMethod>(p.PaymentMethod, out var method) &&
                               (method == PaymentMethod.CreditCard || method == PaymentMethod.DebitCard))
                            {
                                decimal rateFactor = 1 + (surchargePercentage / 100m);
                                total += (p.Amount - (p.Amount / rateFactor));
                            }
                        }
                    }

                    // 4. Validar Saldo a Favor
                    var balancePayments = dto.Payments?
                        .Where(p => Enum.TryParse<PaymentMethod>(p.PaymentMethod, true, out var pm) && pm == PaymentMethod.CustomerBalance)
                        .ToList();

                    if (balancePayments != null && balancePayments.Any())
                    {
                        decimal totalBalanceUsed = balancePayments.Sum(p => p.Amount);
                        if (client.Balance < totalBalanceUsed)
                            return SalesResult.Fail($"Saldo a favor insuficiente. Actual: {client.Balance:C}");
                        
                        client.Balance -= totalBalanceUsed;
                        _context.Update(client);
                    }

                    // 5. Crear Factura
                    decimal taxRate = _config.GetValue<decimal>("TaxSettings:IVARate");
                    if (taxRate > 1) taxRate /= 100;
                    
                    decimal subtotal = total / (1 + taxRate);
                    var invoice = new Invoice
                    {
                        ClientID = client.ClientID,
                        Client = client,
                        SaleDate = dto.SaleDate,
                        SaleChannel = dto.SaleChannel ?? SaleChannel.InStore,
                        UserId = userId, // Nombre de usuario o ID
                        Subtotal = subtotal,
                        Tax = total - subtotal,
                        Total = total,
                        Status = InvoiceStatus.Completed
                    };
                    _context.Invoices.Add(invoice);
                    await _context.SaveChangesAsync();

                    // 6. Procesar Pagos
                    var payments = new List<Payment>();
                    if (dto.Payments != null)
                    {
                        foreach (var paymentVM in dto.Payments)
                        {
                            if (Enum.TryParse<PaymentMethod>(paymentVM.PaymentMethod, out var paymentMethod))
                            {
                                var payment = new Payment
                                {
                                    InvoiceID = invoice.InvoiceID,
                                    PaymentMethod = paymentMethod,
                                    Amount = paymentVM.Amount,
                                    ReferenceNumber = paymentVM.ReferenceNumber,
                                    BankID = paymentVM.BankID,
                                    PaymentDate = dto.SaleDate
                                };
                                payments.Add(payment);
                                _context.Payments.Add(payment);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    // 7. Crear Venta y Detalles
                    var sale = new Sale
                    {
                        ClientID = client.ClientID,
                        Client = client,
                        InvoiceID = invoice.InvoiceID,
                        Invoice = invoice,
                        SaleDate = dto.SaleDate,
                        TotalAmount = total,
                        Details = new List<SaleDetail>()
                    };

                    foreach (var detailVM in dto.Details)
                    {
                        var product = productsFromDb[detailVM.ProductID]; // Se garantiza que existe por la validación previa
                        var detail = new SaleDetail
                        {
                            ProductID = detailVM.ProductID,
                            Product = product,
                            Quantity = detailVM.Quantity,
                            UnitPrice = detailVM.UnitPrice,
                            Cost = product.Cost,
                            Sale = sale,
                            IMEI = detailVM.IMEI?.ToUpper(),
                            Serial = detailVM.Serial?.ToUpper()
                        };
                        sale.Details.Add(detail);

                        // Actualizar Inventario y Kardex
                        var inventory = inventoriesFromDb[detail.ProductID];
                        inventory.Stock -= detail.Quantity;
                        
                        _context.Set<InventoryMovement>().Add(new InventoryMovement
                        {
                            ProductId = detail.ProductID,
                            Date = dto.SaleDate,
                            Type = InventoryMovementType.Sale,
                            Quantity = -detail.Quantity,
                            UnitCost = detail.Cost,
                            StockBalance = (int)inventory.Stock,
                            Reference = $"Venta #{invoice.InvoiceID}",
                            UserId = userId
                        });
                    }
                    _context.Sales.Add(sale);
                    await _context.SaveChangesAsync();

                    // 8. Registrar Movimientos Financieros (Caja/Bancos)
                    foreach (var p in payments)
                    {
                        if (p.PaymentMethod == PaymentMethod.Cash)
                            await _cashService.RegisterIncomeAsync(p.Amount, $"Pago venta #{invoice.InvoiceID}", userId, p.PaymentID);
                        else if (p.BankID.HasValue && p.PaymentMethod != PaymentMethod.CreditCard)
                            await _bankService.RegisterIncomeFromPaymentAsync(p);
                        else if (p.PaymentMethod == PaymentMethod.CreditCard && p.BankID.HasValue)
                            _context.CreditCardVerifications.Add(new CreditCardVerification { SaleID = sale.SaleID, PaymentID = p.PaymentID, BankID = p.BankID.Value, Amount = p.Amount, CreationDate = DateTime.Now, Status = VerificationStatus.Pending });
                    }
                    await _context.SaveChangesAsync();

                    await _auditService.LogAsync("Ventas", $"Registró venta #{invoice.InvoiceID} por {total:C}.");
                    await transaction.CommitAsync();

                    return SalesResult.Ok(invoice.InvoiceID);
                }
                catch (Exception ex) { await transaction.RollbackAsync(); return SalesResult.Fail(ex.Message); }
            });
        }
    }
}