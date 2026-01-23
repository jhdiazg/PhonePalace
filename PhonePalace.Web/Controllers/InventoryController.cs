﻿﻿﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.Helpers;
using System.Linq;
using System.Threading.Tasks;
using PhonePalace.Domain.Entities;

namespace PhonePalace.Web.Controllers
{
    public class InventoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InventoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Route("Inventario")]
        public async Task<IActionResult> Index(string? searchString, int? categoryId, int? pageNumber, int? pageSize)
        {
            ViewData["PageSize"] = pageSize ?? 10;
            ViewData["CurrentFilter"] = searchString;
            ViewData["CategoryID"] = new SelectList(_context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).AsNoTracking(), "CategoryID", "Name", categoryId);
            ViewData["CurrentCategory"] = categoryId;

            var inventoryQuery = _context.Inventories.AsQueryable();

            if (categoryId.HasValue)
            {
                inventoryQuery = inventoryQuery.Where(i => i.Product.CategoryID == categoryId);
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                inventoryQuery = inventoryQuery.Where(i => i.Product.Name.Contains(searchString) || (i.Product.SKU != null && i.Product.SKU.Contains(searchString)) || (i.Product.Code != null && i.Product.Code.Contains(searchString)));
            }

            var inventoryReportQuery = inventoryQuery
                .Include(i => i.Product)
                .Select(i => new PhonePalace.Web.ViewModels.InventoryReportItemViewModel
                {
                    ProductID = i.ProductID,
                    ProductName = i.Product.Name,
                    ProductSKU = i.Product.SKU ?? string.Empty,
                    CurrentStock = i.Stock,
                    LastUpdated = i.LastUpdated,
                    TotalPurchases = _context.PurchaseDetails
                        .Where(pd => pd.ProductId == i.ProductID && pd.Purchase != null && pd.Purchase.Status == Domain.Enums.PurchaseStatus.Received)
                        .Sum(pd => pd.Quantity),
                    TotalSales = _context.InvoiceDetails
                        .Where(id => id.ProductID == i.ProductID && id.Invoice != null && id.Invoice.Status == Domain.Enums.InvoiceStatus.Completed)
                        .Sum(id => id.Quantity)
                })
                .OrderBy(item => item.ProductName)
                .AsNoTracking();

            var paginatedList = await PaginatedList<PhonePalace.Web.ViewModels.InventoryReportItemViewModel>.CreateAsync(inventoryReportQuery, pageNumber ?? 1, pageSize ?? 10);
            return View(paginatedList);
        }
    }
}