using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhonePalace.Infrastructure.Data;
using Microsoft.Extensions.Options;
using PhonePalace.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using PhonePalace.Web.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using PhonePalace.Web.Helpers;
using PhonePalace.Domain.Entities;
using System;
using System.Collections.Generic;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Administrador,Almacenista")]
    public class PurchasesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _webHostEnvironment;
        private readonly IConfiguration _config;
        private readonly IAuditService _auditService;
        private readonly CompanySettings _companySettings;

        public PurchasesController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, IConfiguration config, IAuditService auditService, IOptions<CompanySettings> companySettings)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _config = config;
            _auditService = auditService;
            _companySettings = companySettings.Value;
        }

        [Route("Compras")]
        public async Task<IActionResult> Index(string? searchString, DateTime? startDate, DateTime? endDate, PurchaseStatus? status, int? pageNumber, int? pageSize)
        {
            // 1. Validaciones de fechas (Servidor)
            bool datesAdjusted = false;
            if (startDate.HasValue && startDate.Value.Date > DateTime.Now.Date)
            {
                startDate = DateTime.Now.Date;
                datesAdjusted = true;
            }
            if (endDate.HasValue && startDate.HasValue && endDate.Value.Date < startDate.Value.Date)
            {
                endDate = startDate;
                datesAdjusted = true;
            }

            if (datesAdjusted)
            {
                TempData["Info"] = "Las fechas han sido ajustadas para cumplir con las validaciones.";
                return RedirectToAction("Index", new { searchString, startDate = startDate?.ToString("yyyy-MM-dd"), endDate = endDate?.ToString("yyyy-MM-dd"), status, pageNumber, pageSize });
            }

            ViewData["SearchString"] = searchString;
            ViewData["CurrentFilter"] = searchString;
            ViewData["StartDate"] = startDate?.ToString("yyyy-MM-dd");
            ViewData["EndDate"] = endDate?.ToString("yyyy-MM-dd");
            ViewData["Status"] = status;
            ViewData["PageSize"] = pageSize ?? 10;

            var purchasesQuery = _context.Purchases
                .Include(p => p.Supplier)
                .AsQueryable();

            // Valores por defecto: Si no hay filtros, usar fecha actual
            if (!startDate.HasValue && !endDate.HasValue && string.IsNullOrEmpty(searchString) && !status.HasValue)
            {
                startDate = DateTime.Now.Date;
                endDate = DateTime.Now.Date;
                ViewData["StartDate"] = startDate?.ToString("yyyy-MM-dd");
                ViewData["EndDate"] = endDate?.ToString("yyyy-MM-dd");
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.Trim();
                bool isNumeric = int.TryParse(searchString, out int searchId);

                purchasesQuery = purchasesQuery.Where(p => 
                    (isNumeric && p.Id == searchId) ||
                    p.Id.ToString().Contains(searchString) ||
                    (p.Supplier is NaturalPersonSupplier && (
                        ((NaturalPersonSupplier)p.Supplier).FirstName.Contains(searchString) || 
                        ((NaturalPersonSupplier)p.Supplier).LastName.Contains(searchString) ||
                        ((NaturalPersonSupplier)p.Supplier).DocumentNumber.Contains(searchString) ||
                        (((NaturalPersonSupplier)p.Supplier).FirstName + " " + ((NaturalPersonSupplier)p.Supplier).LastName).Contains(searchString))) ||
                    (p.Supplier is LegalEntitySupplier && (
                        ((LegalEntitySupplier)p.Supplier).CompanyName.Contains(searchString) ||
                        ((LegalEntitySupplier)p.Supplier).NIT.Contains(searchString))) ||
                    (p.SupplierInvoiceNumber != null && p.SupplierInvoiceNumber.Contains(searchString))
                );
            }

            if (startDate.HasValue)
            {
                purchasesQuery = purchasesQuery.Where(p => p.PurchaseDate.Date >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                purchasesQuery = purchasesQuery.Where(p => p.PurchaseDate.Date <= endDate.Value.Date);
            }

            if (status.HasValue)
            {
                purchasesQuery = purchasesQuery.Where(p => p.Status == status.Value);
            }

            purchasesQuery = purchasesQuery.OrderByDescending(p => p.PurchaseDate);

            var paginatedPurchases = await PaginatedList<Purchase>.CreateAsync(purchasesQuery.AsNoTracking(), pageNumber ?? 1, pageSize ?? 10);

            return View(paginatedPurchases);
        }

        [Route("Compras/Crear")]
        public async Task<IActionResult> Create()
        {
            var viewModel = new PurchaseCreateViewModel
            {
                Suppliers = new SelectList(await _context.Suppliers.ToListAsync(), "SupplierID", "DisplayName"),
                Categories = new SelectList(await _context.Categories.ToListAsync(), "CategoryID", "Name"),
                IVARate = _config.GetValue<decimal>("TaxSettings:IVARate"),
            };
            var products = await _context.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
            products.ForEach(p => p.Name = $"{p.Name} ({p.SKU})");
            ViewBag.AllProducts = products;
            return View(viewModel);
        }

        [HttpPost]
        [Route("Compras/Crear")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PurchaseCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                                var purchase = new Purchase
                {
                    SupplierId = model.SupplierId,
                    PurchaseDate = DateTime.Now,
                    Status = PurchaseStatus.Draft, // Set initial status to Draft
                    PaymentMethod = model.PaymentMethod,
                    SupplierInvoiceNumber = model.SupplierInvoiceNumber?.ToUpper(),
                    DocumentType = model.DocumentType,
                    Observations = model.Observations,
                    PurchaseDetails = model.Details?.Select(d => 
                    {
                        var taxRate = d.TaxRate;
                        return new PurchaseDetail
                        {
                            ProductId = d.ProductId,
                            Quantity = d.Quantity,
                            UnitPrice = d.UnitPrice,
                            TaxRate = taxRate
                        };
                    }).ToList() ?? new List<PurchaseDetail>()
                };

                purchase.TotalAmount = purchase.PurchaseDetails.Sum(d => d.TotalPrice);

                // Inventory will be updated when the purchase status changes to 'Received'

                _context.Add(purchase);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Compras", $"Creó la compra #{purchase.Id} para el proveedor ID: {purchase.SupplierId} (Pago: {purchase.PaymentMethod}).");
                return RedirectToAction(nameof(Index));
            }

            model.Suppliers = new SelectList(await _context.Suppliers.ToListAsync(), "SupplierID", "DisplayName", model.SupplierId);
            model.Categories = new SelectList(await _context.Categories.ToListAsync(), "CategoryID", "Name");
            model.IVARate = _config.GetValue<decimal>("TaxSettings:IVARate");
            var products = await _context.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
            products.ForEach(p => p.Name = $"{p.Name} ({p.SKU})");
            ViewBag.AllProducts = products;
            return View(model);
        }

        [Route("Compras/Detalles/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var purchase = await _context.Purchases
                .Include(p => p.Supplier)
                .Include(p => p.PurchaseDetails)
                .Include(d => d.PurchaseDetails)
                    .ThenInclude(d => d.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (purchase == null)
            {
                return NotFound();
            }

            return View(purchase);
        }

        [Route("Compras/Editar/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var purchase = await _context.Purchases
                .Include(p => p.PurchaseDetails)
                .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (purchase == null)
            {
                return NotFound();
            }

            if (purchase.Status != PurchaseStatus.Draft)
            {
                TempData["ErrorMessage"] = "Solo se pueden editar compras en estado Borrador.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var products = await _context.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
            products.ForEach(p => p.Name = $"{p.Name} ({p.SKU})");

            var viewModel = new PurchaseEditViewModel
            {
                Id = purchase.Id,
                SupplierId = purchase.SupplierId,
                Suppliers = new SelectList(await _context.Suppliers.ToListAsync(), "SupplierID", "DisplayName", purchase.SupplierId),
                Products = new SelectList(products, "ProductID", "Name"),
                IVARate = _config.GetValue<decimal>("TaxSettings:IVARate"),
                PaymentMethod = purchase.PaymentMethod,
                SupplierInvoiceNumber = purchase.SupplierInvoiceNumber,
                DocumentType = purchase.DocumentType,
                Observations = purchase.Observations,
                Details = purchase.PurchaseDetails?.Select(d => new PurchaseDetailViewModel
                {
                    ProductId = d.ProductId,
                    ProductName = d.Product?.Name,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    TaxRate = d.TaxRate
                }).ToList() ?? new List<PurchaseDetailViewModel>()
            };

            ViewBag.AllProducts = products;
            return View(viewModel);
        }

        [HttpPost]
        [Route("Compras/Editar/{id?}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PurchaseEditViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var purchase = await _context.Purchases.Include(p => p.PurchaseDetails).FirstOrDefaultAsync(p => p.Id == id);
                    if (purchase == null) return NotFound();

                    

                    _context.PurchaseDetails.RemoveRange(purchase.PurchaseDetails ?? new List<PurchaseDetail>());

                    purchase.SupplierId = model.SupplierId;
                    purchase.PaymentMethod = model.PaymentMethod;
                    purchase.SupplierInvoiceNumber = model.SupplierInvoiceNumber?.ToUpper();
                    purchase.DocumentType = model.DocumentType;
                    purchase.Observations = model.Observations;
                    purchase.PurchaseDetails = model.Details?.Select(d => 
                    {
                        var taxRate = d.TaxRate;
                        return new PurchaseDetail
                        {
                            ProductId = d.ProductId,
                            Quantity = d.Quantity,
                            UnitPrice = d.UnitPrice,
                            TaxRate = taxRate
                        };
                    }).ToList() ?? new List<PurchaseDetail>();

                    purchase.TotalAmount = purchase.PurchaseDetails.Sum(d => d.TotalPrice);

                    

                    _context.Update(purchase);
                    await _context.SaveChangesAsync();
                    await _auditService.LogAsync("Compras", $"Editó la compra #{purchase.Id} (Pago: {purchase.PaymentMethod}).");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Purchases.Any(e => e.Id == model.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            model.Suppliers = new SelectList(await _context.Suppliers.ToListAsync(), "SupplierID", "DisplayName", model.SupplierId);
            var products = await _context.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
            products.ForEach(p => p.Name = $"{p.Name} ({p.SKU})");
            model.Products = new SelectList(products, "ProductID", "Name");
            model.IVARate = _config.GetValue<decimal>("TaxSettings:IVARate");
            ViewBag.AllProducts = products;
            return View(model);
        }

        [HttpPost]
        [Route("Compras/Confirmar/{id?}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmOrder(int id)
        {
            var purchase = await _context.Purchases.FirstOrDefaultAsync(p => p.Id == id);
            if (purchase == null)
            {
                return NotFound();
            }

            if (purchase.Status != PurchaseStatus.Draft)
            {
                TempData["ErrorMessage"] = "Solo se pueden confirmar órdenes en estado Borrador.";
                return RedirectToAction(nameof(Details), new { id });
            }

            purchase.Status = PurchaseStatus.Ordered;
            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Compras", $"Confirmó la orden de compra #{purchase.Id}.");

            TempData["SuccessMessage"] = $"La compra #{purchase.Id} ha sido confirmada y su estado es ahora Ordenada.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [Route("Compras/MarcarComoFacturada/{id?}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsBilled(int id)
        {
            var purchase = await _context.Purchases.FirstOrDefaultAsync(p => p.Id == id);
            if (purchase == null)
            {
                return NotFound();
            }

            if (purchase.Status != PurchaseStatus.Received)
            {
                TempData["ErrorMessage"] = "Solo se pueden marcar como facturadas órdenes en estado Recibida.";
                return RedirectToAction(nameof(Details), new { id });
            }

            purchase.Status = PurchaseStatus.Billed;
            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Compras", $"Marcó como facturada la compra #{purchase.Id}.");

            TempData["SuccessMessage"] = $"La compra #{purchase.Id} ha sido marcada como facturada.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [Route("Compras/MarcarComoPagada/{id?}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            var purchase = await _context.Purchases.FirstOrDefaultAsync(p => p.Id == id);
            if (purchase == null)
            {
                return NotFound();
            }

            // Redirigir al módulo financiero para garantizar la integridad de caja/bancos
            var accountPayable = await _context.AccountPayables.FirstOrDefaultAsync(ap => ap.PurchaseId == id);
            if (accountPayable != null)
            {
                TempData["Info"] = "Para registrar el pago y afectar correctamente la Caja o Bancos, debe hacerlo desde la Cuenta por Pagar.";
                return RedirectToAction("Details", "AccountPayables", new { id = accountPayable.Id });
            }

            // Fallback: Si no hay CxP (ej. compras antiguas o efectivo sin CxP), permitir marcar como pagada directamente
            purchase.Status = PurchaseStatus.Paid;
            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Compras", $"Marcó como pagada la compra #{purchase.Id} (Manual/Sin CxP).");

            TempData["SuccessMessage"] = "La compra ha sido marcada como pagada.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [Route("Compras/Eliminar/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var purchase = await _context.Purchases
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (purchase == null)
            {
                return NotFound();
            }

            return View(purchase);
        }

        [HttpPost, ActionName("Delete")]
        [Route("Compras/Eliminar/{id?}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var purchase = await _context.Purchases.Include(p => p.PurchaseDetails).FirstOrDefaultAsync(p => p.Id == id);
            if (purchase == null) return NotFound();

            // Validar si tiene Cuenta por Pagar asociada
            var accountPayable = await _context.AccountPayables.FirstOrDefaultAsync(ap => ap.PurchaseId == id);
            if (accountPayable != null)
            {
                if (accountPayable.IsPaid)
                {
                    TempData["ErrorMessage"] = "No se puede anular la compra porque tiene una cuenta por pagar pagada. Revierta el pago primero.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                // Si está pendiente, eliminar la CxP al anular la compra
                _context.AccountPayables.Remove(accountPayable);
            }

            if (purchase.Status == PurchaseStatus.Cancelled)
            {
                TempData["ErrorMessage"] = "Esta compra ya ha sido cancelada.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (purchase.Status == PurchaseStatus.Billed || purchase.Status == PurchaseStatus.Paid)
            {
                TempData["ErrorMessage"] = "No se puede anular una compra que ya ha sido facturada o pagada.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (purchase.Status == PurchaseStatus.Received || purchase.Status == PurchaseStatus.PartiallyReceived)
            {
                // Revertir el stock de lo que realmente se recibió (Total o Parcial)
                foreach (var detail in purchase.PurchaseDetails ?? new List<PurchaseDetail>())
                {
                    if (detail.ReceivedQuantity > 0)
                    {
                        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductID == detail.ProductId);
                        if (inventory != null) 
                        {
                            inventory.Stock -= detail.ReceivedQuantity;

                            // Kardex: Salida por Anulación de Compra
                            _context.Set<InventoryMovement>().Add(new InventoryMovement
                            {
                                ProductId = detail.ProductId,
                                Date = DateTime.Now,
                                Type = InventoryMovementType.PurchaseCancellation,
                                Quantity = -detail.ReceivedQuantity,
                                UnitCost = detail.UnitPrice,
                                StockBalance = (int)inventory.Stock,
                                Reference = $"Anulación Compra #{purchase.Id}",
                                UserId = User.Identity?.Name
                            });
                        }
                    }
                }
            }

            purchase.Status = PurchaseStatus.Cancelled;
            purchase.DeletedDate = DateTime.Now;

            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Compras", $"Anuló la compra #{purchase.Id}.");
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Route("Compras/Recibir/{id}")]
        public async Task<IActionResult> Receive(int id)
        {
            var purchase = await _context.Purchases
                .Include(p => p.Supplier)
                .Include(p => p.PurchaseDetails)
                .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (purchase == null) return NotFound();

            if (purchase.Status == PurchaseStatus.Received || purchase.Status == PurchaseStatus.Cancelled)
            {
                TempData["ErrorMessage"] = "Esta compra no se puede recibir (ya fue completada o cancelada).";
                return RedirectToAction(nameof(Details), new { id });
            }

            var viewModel = new PurchaseReceiveViewModel
            {
                PurchaseId = purchase.Id,
                SupplierName = purchase.Supplier?.DisplayName ?? "N/A",
                SupplierInvoiceNumber = purchase.SupplierInvoiceNumber,
                DocumentType = purchase.DocumentType,
                Details = purchase.PurchaseDetails.Select(d => new PurchaseReceiveDetailViewModel
                {
                    PurchaseDetailId = d.Id,
                    ProductName = d.Product?.Name ?? "Desconocido",
                    OrderedQuantity = d.Quantity,
                    PreviouslyReceivedQuantity = d.ReceivedQuantity,
                    QuantityToReceive = d.Quantity - d.ReceivedQuantity, // Por defecto sugiere el restante
                    UnitPrice = d.UnitPrice
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        [Route("Compras/Recibir/{id?}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Receive(int id, PurchaseReceiveViewModel model)
        {
            if (id != model.PurchaseId) return NotFound();

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                return await strategy.ExecuteAsync<IActionResult>(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var purchase = await _context.Purchases
                            .Include(p => p.PurchaseDetails)
                            .ThenInclude(d => d.Product)
                            .FirstOrDefaultAsync(p => p.Id == id);

                        if (purchase == null)
                        {
                            return NotFound();
                        }

                        // Actualizar información de factura del proveedor al momento de recibir
                        if (!string.IsNullOrEmpty(model.SupplierInvoiceNumber))
                        {
                            purchase.SupplierInvoiceNumber = model.SupplierInvoiceNumber.ToUpper();
                        }
                        purchase.DocumentType = model.DocumentType;

                        decimal receivedAmount = 0;
                        bool anyItemReceived = false;

                        // Cargar manualmente los InventoryLevels para cada producto
                        foreach (var itemModel in model.Details)
                        {
                            var detail = purchase.PurchaseDetails.FirstOrDefault(d => d.Id == itemModel.PurchaseDetailId);
                            if (detail != null && detail.Product != null && itemModel.QuantityToReceive > 0)
                            {
                                // Validar que no se reciba más de lo ordenado
                                if (detail.ReceivedQuantity + itemModel.QuantityToReceive > detail.Quantity)
                                {
                                    throw new InvalidOperationException($"No puede recibir más de lo ordenado para el producto {detail.Product.Name}.");
                                }

                                anyItemReceived = true;
                                
                                // 1. Cargar inventarios existentes desde la BD al contexto (ChangeTracker)
                                await _context.Inventories.Where(i => i.ProductID == detail.ProductId).LoadAsync();
                                
                                // 2. Consultar desde .Local para incluir tanto los de BD como los NUEVOS agregados en iteraciones previas de este ciclo
                                var inventoryLevels = _context.Inventories.Local.Where(i => i.ProductID == detail.ProductId).ToList();

                                // Calcular Costo Promedio Ponderado
                                var currentStock = inventoryLevels.Sum(i => i.Stock);
                                var currentCost = detail.Product.Cost;
                                var incomingQuantity = itemModel.QuantityToReceive;
                                var incomingCost = detail.UnitPrice; // Costo entrante sin IVA

                                if (currentStock + incomingQuantity > 0)
                                {
                                    var newCost = ((currentStock * currentCost) + (incomingQuantity * incomingCost)) / (currentStock + incomingQuantity);
                                    detail.Product.Cost = newCost;
                                    _context.Update(detail.Product);
                                }

                                var inventory = inventoryLevels.FirstOrDefault();
                                if (inventory != null)
                                {
                                    inventory.Stock += itemModel.QuantityToReceive;
                                    inventory.LastUpdated = DateTime.Now;
                                }
                                else
                                {
                                    _context.Inventories.Add(new Inventory { ProductID = detail.ProductId, Stock = itemModel.QuantityToReceive, LastUpdated = DateTime.Now });
                                }

                                // Actualizar cantidad recibida en el detalle
                                detail.ReceivedQuantity += itemModel.QuantityToReceive;
                                
                                // Calcular monto de lo recibido para la cuenta por pagar (incluyendo IVA proporcional si aplica)
                                // Nota: Usamos el TaxRate del detalle para calcular el total de esta recepción
                                decimal lineTotal = itemModel.QuantityToReceive * detail.UnitPrice;
                                decimal lineTax = lineTotal * detail.TaxRate / 100;
                                receivedAmount += lineTotal + lineTax;
                            }
                        }

                        if (!anyItemReceived)
                        {
                            // Nota: No podemos retornar View(model) directamente dentro de ExecuteAsync si queremos reintentos limpios,
                            // pero para errores de validación lógica está bien lanzar excepción o manejarlo así.
                            // Aquí lanzamos excepción para que caiga en el catch externo y muestre el error.
                            throw new InvalidOperationException("Debe recibir al menos un producto.");
                        }

                        // Determinar el nuevo estado de la compra
                        bool allFullyReceived = purchase.PurchaseDetails.All(d => d.ReceivedQuantity >= d.Quantity);
                        purchase.Status = allFullyReceived ? PurchaseStatus.Received : PurchaseStatus.PartiallyReceived;

                        // Create AccountPayable entry
                        // Generar Cuenta por Pagar para todas las compras (incluso Efectivo) para gestionar el pago en CxP
                        if (receivedAmount > 0)
                        {
                            var accountPayable = new AccountPayable
                            {
                                PurchaseId = purchase.Id,
                                Purchase = purchase,
                                Amount = receivedAmount, // Solo el monto de lo recibido en esta transacción
                                DueDate = purchase.PaymentMethod == PurchasePaymentMethod.Credit ? DateTime.Now.AddDays(30) : DateTime.Now,
                                IsPaid = false,
                                DocumentType = model.DocumentType, 
                                DocumentNumber = !string.IsNullOrEmpty(model.SupplierInvoiceNumber) ? model.SupplierInvoiceNumber : purchase.Id.ToString() + (purchase.Status == PurchaseStatus.PartiallyReceived ? "-P" : ""),
                                CreatedDate = DateTime.Now
                            };
                            _context.AccountPayables.Add(accountPayable);
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        
                        string statusMsg = purchase.Status == PurchaseStatus.Received ? "Totalmente Recibida" : "Parcialmente Recibida";
                        await _auditService.LogAsync("Compras", $"Recepción de compra #{purchase.Id} ({statusMsg}). Inventario actualizado.");

                        TempData["SuccessMessage"] = $"La recepción se procesó correctamente. Estado: {statusMsg}.";
                        return RedirectToAction(nameof(Details), new { id });
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ocurrió un error inesperado al recibir la compra: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [Route("Compras/GenerarPdf/{id}")]
        public async Task<IActionResult> GeneratePurchaseOrderPdf(int id)
        {
            var purchase = await _context.Purchases
                .Include(p => p.Supplier)
                    .ThenInclude(s => s!.Department)
                .Include(p => p.Supplier)
                    .ThenInclude(s => s!.Municipality)
                .Include(p => p.PurchaseDetails)
                    .ThenInclude(d => d.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (purchase == null)
            {
                return NotFound();
            }

            string wwwRootPath = _webHostEnvironment.WebRootPath;
            string logoPath = Path.Combine(wwwRootPath, "images", "Logo_pdf.jpg");
            byte[] logoBytes = await System.IO.File.ReadAllBytesAsync(logoPath);

            var pdfGenerator = new PhonePalace.Web.Documents.PurchasePdfDocument(purchase, logoBytes);
            var pdfBytes = pdfGenerator.GeneratePdf();

            return File(pdfBytes, "application/pdf", $"OrdenDeCompra-{id}.pdf");
        }
    }
}