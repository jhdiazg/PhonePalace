using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PhonePalace.Domain.Enums;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Administrador,Vendedor")]
    public class ReturnsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IPlemsiService _plemsiService;

        public ReturnsController(ApplicationDbContext context, IAuditService auditService, IPlemsiService plemsiService)
        {
            _context = context;
            _auditService = auditService;
            _plemsiService = plemsiService;
        }

        [HttpGet]
        public async Task<IActionResult> Create(int saleId)
        {
            var sale = await _context.Sales
                .Include(s => s.Details).ThenInclude(d => d.Product)
                .Include(s => s.Client)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SaleID == saleId);

            if (sale == null) return NotFound();

            // Obtener devoluciones previas para calcular cuánto se puede devolver
            var previousReturns = await _context.Set<Return>()
                .Include(r => r.Details)
                .Where(r => r.SaleID == saleId)
                .AsNoTracking()
                .ToListAsync();

            var viewModel = new ReturnCreateViewModel
            {
                SaleID = sale.SaleID,
                ClientName = sale.Client.DisplayName,
                SaleDate = sale.SaleDate,
                Details = sale.Details.Select(d => {
                    var returnedQty = previousReturns.SelectMany(r => r.Details)
                                        .Where(rd => rd.ProductID == d.ProductID)
                                        .Sum(rd => rd.Quantity);
                    return new ReturnDetailViewModel
                    {
                        ProductID = d.ProductID,
                        ProductName = d.Product.Name,
                        SoldQuantity = d.Quantity,
                        PreviouslyReturned = returnedQty,
                        UnitPrice = d.UnitPrice,
                        QuantityToReturn = 0 // Por defecto 0
                    };
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReturnCreateViewModel model)
        {
            // Filtrar solo los productos que se van a devolver
            var itemsToReturn = model.Details.Where(d => d.QuantityToReturn > 0).ToList();

            if (!itemsToReturn.Any())
            {
                ModelState.AddModelError("", "Debe seleccionar al menos un producto para devolver.");
                return View(model);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var sale = await _context.Sales
                        .Include(s => s.Client)
                        .Include(s => s.Invoice) // Necesario para ID de factura
                        .Include(s => s.Details).ThenInclude(d => d.Product) // Necesario para nombres de productos
                        .FirstOrDefaultAsync(s => s.SaleID == model.SaleID);
                    if (sale == null) throw new Exception("Venta no encontrada.");

                    // 1. SEGURIDAD: Consultar historial real de devoluciones en BD para evitar duplicados
                    var previousReturns = await _context.Returns
                        .Include(r => r.Details)
                        .Where(r => r.SaleID == model.SaleID)
                        .ToListAsync();

                    var returnEntity = new Return
                    {
                        SaleID = model.SaleID,
                        ClientID = sale.ClientID,
                        Date = DateTime.Now,
                        Details = new List<ReturnDetail>()
                    };

                    decimal totalRefund = 0;

                    foreach (var item in itemsToReturn)
                    {
                        var originalSaleDetail = sale.Details.FirstOrDefault(d => d.ProductID == item.ProductID);
                        if (originalSaleDetail == null) throw new Exception($"El producto {item.ProductName} no pertenece a esta venta.");

                        // 2. SEGURIDAD: Calcular lo que ya se ha devuelto anteriormente
                        var previouslyReturnedQty = previousReturns
                            .SelectMany(r => r.Details)
                            .Where(d => d.ProductID == item.ProductID)
                            .Sum(d => d.Quantity);

                        // 3. SEGURIDAD: Validar que la nueva devolución no exceda lo disponible
                        if (item.QuantityToReturn > (originalSaleDetail.Quantity - previouslyReturnedQty))
                        {
                            throw new Exception($"Error de seguridad: Intenta devolver {item.QuantityToReturn} unidades de '{item.ProductName}', pero solo quedan {originalSaleDetail.Quantity - previouslyReturnedQty} disponibles para devolución.");
                        }

                        // 1. Aumentar Inventario
                        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductID == item.ProductID);
                        if (inventory != null)
                        {
                            inventory.Stock += item.QuantityToReturn;
                            inventory.LastUpdated = DateTime.Now;
                            _context.Update(inventory);

                            // 4. KARDEX: Registrar el movimiento en el historial
                            _context.Set<InventoryMovement>().Add(new InventoryMovement
                            {
                                ProductId = item.ProductID,
                                Date = DateTime.Now,
                                Type = InventoryMovementType.Return,
                                Quantity = item.QuantityToReturn,
                                UnitCost = originalSaleDetail.Cost,
                                StockBalance = (int)inventory.Stock,
                                Reference = $"Devolución Venta #{model.SaleID}",
                                UserId = User.Identity?.Name
                            });
                        }

                        // 2. Crear Detalle
                        returnEntity.Details.Add(new ReturnDetail
                        {
                            ProductID = item.ProductID,
                            Quantity = item.QuantityToReturn,
                            UnitPrice = item.UnitPrice,
                            Cost = originalSaleDetail?.Cost ?? 0
                        });

                        totalRefund += (item.QuantityToReturn * item.UnitPrice);
                    }

                    returnEntity.TotalAmount = totalRefund;
                    _context.Add(returnEntity); // Usamos Add genérico o Set<Return>().Add si no está en el contexto tipado

                    // 3. Aumentar Saldo del Cliente (Wallet)
                    var client = await _context.Clients.FindAsync(sale.ClientID);
                    if (client != null)
                    {
                        client.Balance += totalRefund;
                        _context.Update(client);
                    }

                    await _context.SaveChangesAsync();
                    await _auditService.LogAsync("Devoluciones", $"Registró devolución para Venta #{model.SaleID}. Monto abonado a cliente: {totalRefund:C}");
                    
                    // 4. Emitir Nota Crédito Electrónica Parcial si aplica
                    if (sale.Invoice != null)
                    {
                        var electronicInvoice = await _context.Set<ElectronicInvoice>()
                            .FirstOrDefaultAsync(e => e.InvoiceID == sale.Invoice.InvoiceID && e.Status == "Accepted");

                        if (electronicInvoice != null)
                        {
                            try 
                            {
                                var ncResponse = await _plemsiService.SendPartialCreditNoteAsync(returnEntity, sale, "Devolución parcial de productos", electronicInvoice.CUFE);
                                if (ncResponse.Success)
                                {
                                    await _auditService.LogAsync("Facturación", $"Nota Crédito Parcial emitida para factura {sale.Invoice.InvoiceID}. Número: {ncResponse.Number}");
                                    TempData["Info"] = $"Se generó la Nota Crédito {ncResponse.Number} en la DIAN.";
                                }
                                else
                                {
                                    await _auditService.LogAsync("Error Facturación", $"Fallo al emitir Nota Crédito Parcial para {sale.Invoice.InvoiceID}: {ncResponse.ErrorMessage}");
                                }
                            }
                            catch (Exception) { /* Loguear error pero no detener la devolución local */ }
                        }
                    }

                    await transaction.CommitAsync();
                    TempData["Success"] = $"Devolución exitosa. Se han abonado {totalRefund:C} al saldo del cliente.";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", $"Error al procesar la devolución: {ex.Message}");
                }
            });

            if (!ModelState.IsValid) return View(model);

            return RedirectToAction("Details", "Sales", new { id = model.SaleID });
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var returns = await _context.Returns
                .Include(r => r.Client)
                .Include(r => r.Sale)
                .OrderByDescending(r => r.Date)
                .AsNoTracking()
                .ToListAsync();

            return View(returns);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var returnEntity = await _context.Returns
                .Include(r => r.Client)
                .Include(r => r.Sale)
                .Include(r => r.Details)
                    .ThenInclude(rd => rd.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ReturnID == id);

            if (returnEntity == null) return NotFound();

            return View(returnEntity);
        }
    }
}