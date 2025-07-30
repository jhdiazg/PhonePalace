﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Admin,Almacenista")] // Solo Admin y Almacenista pueden gestionar inventario
    public class InventoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public InventoryController(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        // GET: Inventory
        public async Task<IActionResult> Index(string searchString)
        {
            ViewData["CurrentFilter"] = searchString;

            // Consulta que une Productos con su Inventario (si existe)
            var productStockQuery = from p in _context.Products
                                    join inv in _context.Inventories on p.ProductID equals inv.ProductID into productInventory
                                    where p.IsActive
                                    from inv in productInventory.DefaultIfEmpty() // LEFT JOIN
                                    select new InventoryIndexViewModel
                                    {
                                        ProductID = p.ProductID,
                                        SKU = p.SKU,
                                        ProductName = p.Name,
                                        Cost = p.Cost,
                                        Price = p.Price,
                                        Stock = inv == null ? 0 : inv.Stock,
                                        LastUpdated = inv == null ? (DateTime?)null : inv.LastUpdated
                                    };

            if (!string.IsNullOrEmpty(searchString))
            {
                productStockQuery = productStockQuery.Where(p => p.ProductName.Contains(searchString) || (p.SKU != null && p.SKU.Contains(searchString)));
            }

            var productStockList = await productStockQuery.OrderBy(p => p.ProductName).ToListAsync();

            return View(productStockList);
        }

        // GET: Inventory/Adjust/5
        public async Task<IActionResult> Adjust(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductID == id.Value);
            if (product == null)
            {
                return NotFound();
            }

            var inventory = await _context.Inventories
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.ProductID == id.Value);

            var viewModel = new InventoryAdjustViewModel
            {
                ProductID = product.ProductID,
                ProductName = product.Name,
                CurrentStock = inventory?.Stock ?? 0
            };

            return View(viewModel);
        }

        // POST: Inventory/Adjust
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Adjust(InventoryAdjustViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var product = await _context.Products.FindAsync(viewModel.ProductID);
                if (product == null) return NotFound();

                var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductID == viewModel.ProductID);
                int oldStock = inventory?.Stock ?? 0;

                if (inventory == null)
                {
                    inventory = new Inventory { ProductID = viewModel.ProductID };
                    _context.Inventories.Add(inventory);
                }

                inventory.Stock = viewModel.NewStock;
                inventory.LastUpdated = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Inventory", $"Ajuste manual de stock para '{product.Name}' (SKU: {product.SKU ?? "N/A"}). Cantidad: {oldStock} -> {viewModel.NewStock}. Motivo: {viewModel.Reason}");

                return RedirectToAction(nameof(Index));
            }

            var productForView = await _context.Products.FindAsync(viewModel.ProductID);
            viewModel.ProductName = productForView?.Name ?? "Producto no encontrado";
            return View(viewModel);
        }
    }
}