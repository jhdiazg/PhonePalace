﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.Helpers;
using System.Linq;
using System.Threading.Tasks;
using PhonePalace.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using PhonePalace.Domain.Interfaces;
using System;
using Microsoft.AspNetCore.Hosting;
using PhonePalace.Domain.Enums;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Administrador,Almacenista,Vendedor")]
    public class InventoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public InventoryController(ApplicationDbContext context, IAuditService auditService, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _auditService = auditService;
            _webHostEnvironment = webHostEnvironment;
        }

        [Route("Inventario")]
        public async Task<IActionResult> Index(string? sortOrder, string? searchString, int? categoryId, bool showActiveOnly = true, int? pageNumber = null, int? pageSize = null)
        {
            ViewData["PageSize"] = pageSize ?? 10;
            ViewData["CurrentFilter"] = searchString;
            ViewData["CategoryID"] = new SelectList(_context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).AsNoTracking(), "CategoryID", "Name", categoryId);
            ViewData["CurrentCategory"] = categoryId;
            ViewData["CurrentSort"] = sortOrder;
            // Por defecto (null) ordena descendente en días (más antiguos primero = fecha ascendente)
            ViewData["DaysSortParm"] = string.IsNullOrEmpty(sortOrder) ? "days_desc" : (sortOrder == "days_desc" ? "days_asc" : "days_desc");

            ViewData["ShowActiveOnly"] = showActiveOnly;

            var inventoryQuery = _context.Inventories.AsQueryable();

            if (showActiveOnly)
            {
                inventoryQuery = inventoryQuery.Where(i => i.Product.IsActive);
            }

            if (categoryId.HasValue)
            {
                inventoryQuery = inventoryQuery.Where(i => i.Product.CategoryID == categoryId);
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                inventoryQuery = inventoryQuery.Where(i => i.Product.Name.Contains(searchString) || (i.Product.SKU != null && i.Product.SKU.Contains(searchString)) || (i.Product.Code != null && i.Product.Code.Contains(searchString)));
            }

            // Calcular el valor total del inventario a costo (respetando filtros)
            var totalInventoryValue = await inventoryQuery.SumAsync(i => i.Product.Cost * (decimal)((double)i.Stock));
            ViewData["TotalInventoryValue"] = totalInventoryValue;

            var totalInventorySellValue = await inventoryQuery.SumAsync(i => i.Product.Price * (decimal)((double)i.Stock));
            ViewData["TotalInventorySellValue"] = totalInventorySellValue;

            var inventoryReportQuery = inventoryQuery
                .Include(i => i.Product)
                .Select(i => new PhonePalace.Web.ViewModels.InventoryReportItemViewModel
                {
                    ProductID = i.ProductID,
                    ProductName = i.Product.Name,
                    ProductSKU = i.Product.SKU ?? string.Empty,
                    CurrentStock = (int)((double)i.Stock),
                    LastUpdated = i.LastUpdated,
                    TotalPurchases = (int)_context.PurchaseDetails
                        .Where(pd => pd.ProductId == i.ProductID && pd.Purchase != null && 
                                     pd.Purchase.Status != Domain.Enums.PurchaseStatus.Draft && 
                                     pd.Purchase.Status != Domain.Enums.PurchaseStatus.Cancelled)
                        .Sum(pd => (double)pd.ReceivedQuantity),
                    TotalSales = (int)(_context.Sales
                        .Where(s => !s.IsDeleted)
                        .SelectMany(s => s.Details)
                        .Where(sd => sd.ProductID == i.ProductID)
                        .Sum(sd => (double)sd.Quantity) 
                        - _context.ReturnDetails
                        .Where(rd => rd.ProductID == i.ProductID)
                        .Sum(rd => (double)rd.Quantity))
                });

            switch (sortOrder)
            {
                case "days_desc":
                    // Más días = Fecha más antigua (Ascendente)
                    inventoryReportQuery = inventoryReportQuery.OrderBy(s => s.LastUpdated);
                    break;
                case "days_asc":
                    // Menos días = Fecha más reciente (Descendente)
                    inventoryReportQuery = inventoryReportQuery.OrderByDescending(s => s.LastUpdated);
                    break;
                default:
                    inventoryReportQuery = inventoryReportQuery.OrderBy(s => s.ProductName);
                    break;
            }

            var paginatedList = await PaginatedList<PhonePalace.Web.ViewModels.InventoryReportItemViewModel>.CreateAsync(inventoryReportQuery.AsNoTracking(), pageNumber ?? 1, pageSize ?? 10);
            return View(paginatedList);
        }

        [HttpGet]
        [Authorize(Roles = "Administrador")]
        [Route("Inventario/Ajustar/{id}")]
        public async Task<IActionResult> Adjust(int? id)
        {
            if (id == null) return NotFound();

            // Proyectamos a un ViewModel para evitar errores de mapeo con la columna InventoryID/Id
            var inventory = await _context.Inventories
                .Where(i => i.ProductID == id)
                .Select(i => new PhonePalace.Web.ViewModels.InventoryAdjustViewModel
                {
                    ProductID = i.ProductID,
                    ProductName = i.Product.Name,
                    ProductSKU = i.Product.SKU ?? string.Empty,
                    CurrentStock = (int)((double)i.Stock)
                })
                .FirstOrDefaultAsync();

            if (inventory == null) return NotFound();

            return View(inventory);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        [Route("Inventario/Ajustar/{id?}")]
        public async Task<IActionResult> Adjust(int id, int adjustment, string reason)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<IActionResult>(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Obtenemos datos actuales para el log (usando proyección para evitar error de PK)
                    var currentData = await _context.Inventories
                        .Where(i => i.ProductID == id)
                        .Select(i => new { Stock = (int)((double)i.Stock), ProductName = i.Product.Name, Cost = i.Product.Cost })
                        .FirstOrDefaultAsync();

                    if (currentData == null) return NotFound();

                    // Usamos SQL directo para actualizar, evitando problemas de mapeo de Entity Framework
                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE Inventories SET Stock = Stock + {0}, LastUpdated = {1} WHERE ProductID = {2}", 
                        adjustment, DateTime.Now, id);

                    var newStock = currentData.Stock + adjustment;

                    // Registrar Movimiento en Kardex
                    var movement = new InventoryMovement
                    {
                        ProductId = id,
                        Date = DateTime.Now,
                        Type = InventoryMovementType.Adjustment,
                        Quantity = adjustment,
                        UnitCost = currentData.Cost,
                        StockBalance = newStock,
                        Reference = reason ?? "Ajuste Manual",
                        UserId = User.Identity?.Name
                    };
                    _context.InventoryMovements.Add(movement);
                    await _context.SaveChangesAsync();

                    await _auditService.LogAsync("Inventario", $"Ajuste manual de stock para '{currentData.ProductName}' (ID: {id}). Anterior: {currentData.Stock}, Ajuste: {adjustment:+0;-0}, Nuevo: {newStock}. Razón: {reason}");

                    await transaction.CommitAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        [HttpGet]
        [Route("Inventario/Imprimir")]
        public async Task<IActionResult> PrintInventory(string? searchString, int? categoryId, bool showActiveOnly = true)
        {
            var inventoryQuery = _context.Inventories
                .Include(i => i.Product)
                .AsQueryable();

            if (showActiveOnly)
            {
                inventoryQuery = inventoryQuery.Where(i => i.Product.IsActive);
            }

            if (categoryId.HasValue)
            {
                inventoryQuery = inventoryQuery.Where(i => i.Product.CategoryID == categoryId);
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                inventoryQuery = inventoryQuery.Where(i => i.Product.Name.Contains(searchString) || (i.Product.SKU != null && i.Product.SKU.Contains(searchString)) || (i.Product.Code != null && i.Product.Code.Contains(searchString)));
            }

            var items = await inventoryQuery
                .OrderBy(i => i.Product.Name)
                .Select(i => new PhonePalace.Web.ViewModels.InventoryReportItemViewModel
                {
                    ProductID = i.ProductID,
                    ProductName = i.Product.Name,
                    ProductSKU = i.Product.SKU ?? string.Empty,
                    CurrentStock = (int)((double)i.Stock)
                })
                .ToListAsync();

            string wwwRootPath = _webHostEnvironment.WebRootPath;
            string logoPath = System.IO.Path.Combine(wwwRootPath, "images", "Logo_fact.png");
            
            byte[] logoBytes = Array.Empty<byte>();
            if (System.IO.File.Exists(logoPath))
            {
                logoBytes = await System.IO.File.ReadAllBytesAsync(logoPath);
            }

            var document = new PhonePalace.Web.Documents.InventoryPdfDocument(items, logoBytes);
            var pdfBytes = document.GeneratePdf();

            return File(pdfBytes, "application/pdf", $"Inventario_Fisico_{DateTime.Now:yyyyMMdd}.pdf");
        }

        [HttpGet]
        [Route("Inventario/Kardex/{productId}")]
        public async Task<IActionResult> Movements(int productId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound();

            var movements = await _context.InventoryMovements
                .Where(m => m.ProductId == productId)
                .OrderByDescending(m => m.Date)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.ProductName = product.Name;
            ViewBag.ProductSKU = product.SKU;

            return View(movements);
        }
    }
}