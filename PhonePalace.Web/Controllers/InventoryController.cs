﻿using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> Index(int? pageNumber)
        {
            var inventoryReportQuery = _context.Inventories
                .Include(i => i.Product)
                .Select(i => new PhonePalace.Web.ViewModels.InventoryReportItemViewModel
                {
                    ProductID = i.ProductID,
                    ProductName = i.Product.Name,
                    ProductSKU = i.Product.SKU,
                    CurrentStock = i.Stock,
                    LastUpdated = i.LastUpdated,
                    TotalPurchases = _context.PurchaseDetails
                        .Where(pd => pd.ProductId == i.ProductID && pd.Purchase.Status == PhonePalace.Domain.Enums.PurchaseStatus.Received)
                        .Sum(pd => pd.Quantity),
                    TotalSales = _context.InvoiceDetails
                        .Where(id => id.ProductID == i.ProductID && id.Invoice.Status == PhonePalace.Domain.Enums.InvoiceStatus.Completed)
                        .Sum(id => id.Quantity)
                })
                .OrderBy(item => item.ProductName)
                .AsNoTracking();

            int pageSize = 15;
            var paginatedList = await PaginatedList<PhonePalace.Web.ViewModels.InventoryReportItemViewModel>.CreateAsync(inventoryReportQuery, pageNumber ?? 1, pageSize);
            return View(paginatedList);
        }
    }
}