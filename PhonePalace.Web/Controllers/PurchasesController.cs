using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhonePalace.Infrastructure.Data;
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

namespace PhonePalace.Web.Controllers
{
    public class PurchasesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _webHostEnvironment;
        private readonly IConfiguration _config;
        private readonly IAuditService _auditService;

        public PurchasesController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, IConfiguration config, IAuditService auditService)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _config = config;
            _auditService = auditService;
        }

        [Route("Compras")]
        public async Task<IActionResult> Index(int? pageNumber)
        {
            var purchasesQuery = _context.Purchases
                .Include(p => p.Supplier)
                .OrderByDescending(p => p.PurchaseDate)
                .AsNoTracking();

            int pageSize = 10;
            var paginatedPurchases = await PaginatedList<Purchase>.CreateAsync(purchasesQuery, pageNumber ?? 1, pageSize);

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
            ViewBag.AllProducts = await _context.Products.Where(p => p.IsActive).Select(p => new { p.ProductID, p.Name, p.Price }).OrderBy(p => p.Name).ToListAsync();
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
                    PurchaseDate = DateTime.UtcNow,
                    Status = Domain.Enums.PurchaseStatus.Draft, // Set initial status to Draft
                    PaymentMethod = model.PaymentMethod,
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
            ViewBag.AllProducts = await _context.Products.Where(p => p.IsActive).Select(p => new { p.ProductID, p.Name, p.Price }).OrderBy(p => p.Name).ToListAsync();
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

            if (purchase.Status != Domain.Enums.PurchaseStatus.Draft)
            {
                TempData["ErrorMessage"] = "Solo se pueden editar compras en estado Borrador.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var viewModel = new PurchaseEditViewModel
            {
                Id = purchase.Id,
                SupplierId = purchase.SupplierId,
                Suppliers = new SelectList(await _context.Suppliers.ToListAsync(), "SupplierID", "DisplayName", purchase.SupplierId),
                Products = new SelectList(await _context.Products.Where(p => p.IsActive).ToListAsync(), "ProductID", "Name"),
                IVARate = _config.GetValue<decimal>("TaxSettings:IVARate"),
                PaymentMethod = purchase.PaymentMethod,
                Details = purchase.PurchaseDetails?.Select(d => new PurchaseDetailViewModel
                {
                    ProductId = d.ProductId,
                    ProductName = d.Product?.Name,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    TaxRate = d.TaxRate
                }).ToList() ?? new List<PurchaseDetailViewModel>()
            };

            ViewBag.AllProducts = await _context.Products.Where(p => p.IsActive).Select(p => new { p.ProductID, p.Name, p.Price }).OrderBy(p => p.Name).ToListAsync();
            return View(viewModel);
        }

        [HttpPost]
        [Route("Compras/Editar/{id}")]
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
            model.Products = new SelectList(await _context.Products.Where(p => p.IsActive).ToListAsync(), "ProductID", "Name");
            model.IVARate = _config.GetValue<decimal>("TaxSettings:IVARate");
            ViewBag.AllProducts = await _context.Products.Where(p => p.IsActive).Select(p => new { p.ProductID, p.Name, p.Price }).OrderBy(p => p.Name).ToListAsync();
            return View(model);
        }

        [HttpPost]
        [Route("Compras/Confirmar/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmOrder(int id)
        {
            var purchase = await _context.Purchases.FirstOrDefaultAsync(p => p.Id == id);
            if (purchase == null)
            {
                return NotFound();
            }

            if (purchase.Status != Domain.Enums.PurchaseStatus.Draft)
            {
                TempData["ErrorMessage"] = "Solo se pueden confirmar órdenes en estado Borrador.";
                return RedirectToAction(nameof(Details), new { id });
            }

            purchase.Status = Domain.Enums.PurchaseStatus.Ordered;
            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Compras", $"Confirmó la orden de compra #{purchase.Id}.");

            TempData["SuccessMessage"] = $"La compra #{purchase.Id} ha sido confirmada y su estado es ahora Ordenada.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [Route("Compras/MarcarComoFacturada/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsBilled(int id)
        {
            var purchase = await _context.Purchases.FirstOrDefaultAsync(p => p.Id == id);
            if (purchase == null)
            {
                return NotFound();
            }

            if (purchase.Status != Domain.Enums.PurchaseStatus.Received)
            {
                TempData["ErrorMessage"] = "Solo se pueden marcar como facturadas órdenes en estado Recibida.";
                return RedirectToAction(nameof(Details), new { id });
            }

            purchase.Status = Domain.Enums.PurchaseStatus.Billed;
            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Compras", $"Marcó como facturada la compra #{purchase.Id}.");

            TempData["SuccessMessage"] = $"La compra #{purchase.Id} ha sido marcada como facturada.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [Route("Compras/MarcarComoPagada/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            var purchase = await _context.Purchases.FirstOrDefaultAsync(p => p.Id == id);
            if (purchase == null)
            {
                return NotFound();
            }

            if (purchase.Status != Domain.Enums.PurchaseStatus.Billed)
            {
                TempData["ErrorMessage"] = "Solo se pueden marcar como pagadas órdenes en estado Facturada.";
                return RedirectToAction(nameof(Details), new { id });
            }

            purchase.Status = Domain.Enums.PurchaseStatus.Paid;

            var accountPayable = await _context.AccountPayables.FirstOrDefaultAsync(ap => ap.PurchaseId == id);
            if (accountPayable != null)
            {
                accountPayable.IsPaid = true;
            }

            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Compras", $"Marcó como pagada la compra #{purchase.Id}.");

            TempData["SuccessMessage"] = $"La compra #{purchase.Id} ha sido marcada como pagada.";
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
        [Route("Compras/Eliminar/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var purchase = await _context.Purchases.Include(p => p.PurchaseDetails).FirstOrDefaultAsync(p => p.Id == id);
            if (purchase == null) return NotFound();

            if (purchase.Status == Domain.Enums.PurchaseStatus.Cancelled)
            {
                TempData["ErrorMessage"] = "Esta compra ya ha sido cancelada.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (purchase.Status == Domain.Enums.PurchaseStatus.Received)
            {
                // Revertir el stock solo si la compra fue recibida
                foreach (var detail in purchase.PurchaseDetails ?? new List<PurchaseDetail>())
                {
                    var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductID == detail.ProductId);
                    if (inventory != null) inventory.Stock -= detail.Quantity;
                }
            }

            purchase.Status = Domain.Enums.PurchaseStatus.Cancelled;
            purchase.DeletedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Compras", $"Anuló la compra #{purchase.Id}.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Route("Compras/Recibir/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Receive(int id)
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

                if (purchase.Status == Domain.Enums.PurchaseStatus.Received)
                {
                    TempData["ErrorMessage"] = "Esta compra ya ha sido recibida.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Cargar manualmente los InventoryLevels para cada producto
                foreach (var detail in purchase.PurchaseDetails ?? new List<PurchaseDetail>())
                {
                    if (detail.Product != null)
                    {
                        var inventoryLevels = await _context.Inventories.Where(i => i.ProductID == detail.ProductId).ToListAsync();
                        
                        // Calcular Costo Promedio Ponderado
                        var currentStock = inventoryLevels.Sum(i => i.Stock);
                        var currentCost = detail.Product.Cost;
                        var incomingQuantity = detail.Quantity;
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
                            inventory.Stock += detail.Quantity;
                            inventory.LastUpdated = DateTime.UtcNow;
                        }
                        else
                        {
                            _context.Inventories.Add(new Inventory { ProductID = detail.ProductId, Stock = detail.Quantity, LastUpdated = DateTime.UtcNow });
                        }
                    }
                }

                // Change the purchase status
                purchase.Status = Domain.Enums.PurchaseStatus.Received;

                // Create AccountPayable entry
                // Generar Cuenta por Pagar si la forma de pago es Crédito o Transferencia
                if (purchase.PaymentMethod == PurchasePaymentMethod.Credit || 
                    purchase.PaymentMethod == PurchasePaymentMethod.Transfer)
                {
                    var accountPayable = new AccountPayable
                    {
                        PurchaseId = purchase.Id,
                        Purchase = purchase,
                        Amount = purchase.TotalAmount,
                        DueDate = DateTime.UtcNow.AddDays(30), // Example: Due in 30 days
                        IsPaid = false,
                        DocumentType = AccountPayableDocumentType.Invoice, // Default to Invoice
                        DocumentNumber = purchase.Id.ToString(), // Use PurchaseId as DocumentNumber for now
                        CreatedDate = DateTime.UtcNow
                    };
                    _context.AccountPayables.Add(accountPayable);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                await _auditService.LogAsync("Compras", $"Recibió la compra #{purchase.Id} (Pago: {purchase.PaymentMethod}) y actualizó el inventario.");

                TempData["SuccessMessage"] = $"La compra #{purchase.Id} ha sido recibida (Pago: {purchase.PaymentMethod}).";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                // _logger.LogError(ex, "Error al recibir la compra con ID {PurchaseID}", id); // Uncomment if _logger is available
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