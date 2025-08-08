using Microsoft.AspNetCore.Mvc;
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

namespace PhonePalace.Web.Controllers
{
    public class PurchasesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _webHostEnvironment;

        public PurchasesController(ApplicationDbContext context, Microsoft.AspNetCore.Hosting.IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
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
            };
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
                    Status = PhonePalace.Domain.Enums.PurchaseStatus.Draft, // Set initial status to Draft
                    PurchaseDetails = model.Details?.Select(d => new PurchaseDetail
                    {
                        ProductId = d.ProductId,
                        Quantity = d.Quantity,
                        UnitPrice = d.UnitPrice
                    }).ToList() ?? new List<PurchaseDetail>()
                };

                purchase.TotalAmount = purchase.PurchaseDetails.Sum(d => d.TotalPrice);

                // Inventory will be updated when the purchase status changes to 'Received'

                _context.Add(purchase);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            model.Suppliers = new SelectList(await _context.Suppliers.ToListAsync(), "SupplierID", "DisplayName", model.SupplierId);
            return View(model);
        }

        [Route("Compras/Detalles/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var purchase = await _context.Purchases
                .Include(p => p.Supplier)
                .Include(p => p.PurchaseDetails)
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
            var purchase = await _context.Purchases.Include(p => p.PurchaseDetails).FirstOrDefaultAsync(p => p.Id == id);
            if (purchase == null)
            {
                return NotFound();
            }

            if (purchase.Status != PhonePalace.Domain.Enums.PurchaseStatus.Draft)
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
                Details = purchase.PurchaseDetails?.Select(d => new PurchaseDetailViewModel
                {
                    ProductId = d.ProductId,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice
                }).ToList() ?? new List<PurchaseDetailViewModel>()
            };

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
                    purchase.PurchaseDetails = model.Details?.Select(d => new PurchaseDetail
                    {
                        ProductId = d.ProductId,
                        Quantity = d.Quantity,
                        UnitPrice = d.UnitPrice
                    }).ToList() ?? new List<PurchaseDetail>();

                    purchase.TotalAmount = purchase.PurchaseDetails.Sum(d => d.TotalPrice);

                    

                    _context.Update(purchase);
                    await _context.SaveChangesAsync();
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

            if (purchase.Status != PhonePalace.Domain.Enums.PurchaseStatus.Draft)
            {
                TempData["ErrorMessage"] = "Solo se pueden confirmar órdenes en estado Borrador.";
                return RedirectToAction(nameof(Details), new { id });
            }

            purchase.Status = PhonePalace.Domain.Enums.PurchaseStatus.Ordered;
            await _context.SaveChangesAsync();

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

            if (purchase.Status != PhonePalace.Domain.Enums.PurchaseStatus.Received)
            {
                TempData["ErrorMessage"] = "Solo se pueden marcar como facturadas órdenes en estado Recibida.";
                return RedirectToAction(nameof(Details), new { id });
            }

            purchase.Status = PhonePalace.Domain.Enums.PurchaseStatus.Billed;
            await _context.SaveChangesAsync();

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

            if (purchase.Status != PhonePalace.Domain.Enums.PurchaseStatus.Billed)
            {
                TempData["ErrorMessage"] = "Solo se pueden marcar como pagadas órdenes en estado Facturada.";
                return RedirectToAction(nameof(Details), new { id });
            }

            purchase.Status = PhonePalace.Domain.Enums.PurchaseStatus.Paid;

            var accountPayable = await _context.AccountPayables.FirstOrDefaultAsync(ap => ap.PurchaseId == id);
            if (accountPayable != null)
            {
                accountPayable.IsPaid = true;
            }

            await _context.SaveChangesAsync();

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

            if (purchase.Status == PhonePalace.Domain.Enums.PurchaseStatus.Cancelled)
            {
                TempData["ErrorMessage"] = "Esta compra ya ha sido cancelada.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (purchase.Status == PhonePalace.Domain.Enums.PurchaseStatus.Received)
            {
                // Revertir el stock solo si la compra fue recibida
                foreach (var detail in purchase.PurchaseDetails ?? new List<PurchaseDetail>())
                {
                    var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductID == detail.ProductId);
                    if (inventory != null) inventory.Stock -= detail.Quantity;
                }
            }

            purchase.Status = PhonePalace.Domain.Enums.PurchaseStatus.Cancelled;
            purchase.DeletedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
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
                        .ThenInclude(d => (Product?)d.Product)
                            .ThenInclude(p => p.InventoryLevels)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (purchase == null)
                {
                    return NotFound();
                }

                if (purchase.Status == PhonePalace.Domain.Enums.PurchaseStatus.Received)
                {
                    TempData["ErrorMessage"] = "Esta compra ya ha sido recibida.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Update inventory for each product in the purchase
                foreach (var detail in purchase.PurchaseDetails ?? new List<PurchaseDetail>())
                {
                    if (detail.Product != null)
                    {
                        var inventory = detail.Product?.InventoryLevels?.FirstOrDefault();
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
                purchase.Status = PhonePalace.Domain.Enums.PurchaseStatus.Received;

                // Create AccountPayable entry
                var accountPayable = new AccountPayable
                {
                    PurchaseId = purchase.Id,
                    Amount = purchase.TotalAmount,
                    DueDate = DateTime.UtcNow.AddDays(30), // Example: Due in 30 days
                    IsPaid = false,
                    DocumentType = PhonePalace.Domain.Enums.AccountPayableDocumentType.Invoice, // Default to Invoice
                    DocumentNumber = purchase.Id.ToString(), // Use PurchaseId as DocumentNumber for now
                    CreatedDate = DateTime.UtcNow
                };
                _context.AccountPayables.Add(accountPayable);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"La compra #{purchase.Id} ha sido recibida exitosamente y el inventario actualizado.";
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