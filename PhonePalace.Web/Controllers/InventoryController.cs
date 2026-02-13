﻿﻿﻿using Microsoft.AspNetCore.Mvc;
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

namespace PhonePalace.Web.Controllers
{
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
                        .Where(pd => pd.ProductId == i.ProductID && pd.Purchase != null && pd.Purchase.Status == Domain.Enums.PurchaseStatus.Received)
                        .Sum(pd => (double)pd.Quantity),
                    TotalSales = (int)_context.Sales
                        .Where(s => !s.IsDeleted)
                        .SelectMany(s => s.Details)
                        .Where(sd => sd.ProductID == i.ProductID)
                        .Sum(sd => (double)sd.Quantity)
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
        [Route("Inventario/Ajustar/{id}")]
        public async Task<IActionResult> Adjust(int id, int adjustment, string reason)
        {
            // Obtenemos datos actuales para el log (usando proyección para evitar error de PK)
            var currentData = await _context.Inventories
                .Where(i => i.ProductID == id)
                .Select(i => new { Stock = (int)((double)i.Stock), ProductName = i.Product.Name })
                .FirstOrDefaultAsync();

            if (currentData == null) return NotFound();

            // Usamos SQL directo para actualizar, evitando problemas de mapeo de Entity Framework
            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE Inventories SET Stock = Stock + {0}, LastUpdated = {1} WHERE ProductID = {2}", 
                adjustment, DateTime.Now, id);

            var newStock = currentData.Stock + adjustment;
            await _auditService.LogAsync("Inventario", $"Ajuste manual de stock para '{currentData.ProductName}' (ID: {id}). Anterior: {currentData.Stock}, Ajuste: {adjustment:+0;-0}, Nuevo: {newStock}. Razón: {reason}");

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Route("Inventario/Imprimir")]
        public async Task<IActionResult> PrintInventory(string? searchString, int? categoryId)
        {
            var inventoryQuery = _context.Inventories
                .Include(i => i.Product)
                .AsQueryable();

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
    }
}