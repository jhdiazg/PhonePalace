﻿﻿﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AccessoriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IFileStorageService _fileStorageService;
        private readonly string _containerName = "products";

        public AccessoriesController(ApplicationDbContext context, IAuditService auditService, IFileStorageService fileStorageService)
        {
            _context = context;
            _auditService = auditService;
            _fileStorageService = fileStorageService;
        }

        // GET: Accessories
        public async Task<IActionResult> Index()
        {
            var accessoriesQuery = _context.Accessories
                .Where(a => a.IsActive)
                .Include(a => a.Images)
                .Include(a => a.Category)
                .Include(a => a.Brand)
                .AsNoTracking();
            return View(await accessoriesQuery.ToListAsync());
        }

        // GET: Accessories/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var accessory = await _context.Accessories
                .Include(a => a.Category)
                .Include(a => a.Images)
                .Include(a => a.Brand)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ProductID == id);
            if (accessory == null)
            {
                return NotFound();
            }

            return View(accessory);
        }

        // GET: Accessories/Create
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View(new AccessoryViewModel());
        }

        // POST: Accessories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AccessoryViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var accessory = new Accessory
                {
                    Name = viewModel.Name,
                    Description = viewModel.Description,
                    Price = viewModel.Price,
                    Cost = viewModel.Cost,
                    SKU = viewModel.SKU,
                    CategoryID = viewModel.CategoryID,
                    BrandID = viewModel.BrandID,
                    Color = viewModel.Color,
                    IsActive = viewModel.IsActive
                };

                if (viewModel.NewImageFile != null)
                {
                    var imageUrl = await _fileStorageService.SaveFileAsync(viewModel.NewImageFile, _containerName);
                    accessory.Images.Add(new ProductImage
                    {
                        ImageUrl = imageUrl,
                        IsPrimary = true
                    });
                }

                _context.Add(accessory);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Accessories", $"Creó el accesorio '{accessory.Name}' (ID: {accessory.ProductID}).");
                return RedirectToAction(nameof(Index));
            }
            await PopulateDropdowns(viewModel.CategoryID, viewModel.BrandID);
            return View(viewModel);
        }

        // GET: Accessories/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var accessory = await _context.Accessories
                .Include(a => a.Images)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ProductID == id);

            if (accessory == null)
            {
                return NotFound();
            }

            var viewModel = new AccessoryViewModel
            {
                ProductID = accessory.ProductID,
                Name = accessory.Name,
                Description = accessory.Description,
                Price = accessory.Price,
                Cost = accessory.Cost,
                SKU = accessory.SKU,
                CategoryID = accessory.CategoryID,
                BrandID = accessory.BrandID,
                Color = accessory.Color,
                IsActive = accessory.IsActive,
                Images = accessory.Images.Select(i => new ProductImageViewModel
                {
                    ProductImageID = i.ProductImageID,
                    ImageUrl = i.ImageUrl,
                    IsPrimary = i.IsPrimary
                }).ToList()
            };

            await PopulateDropdowns(viewModel.CategoryID, viewModel.BrandID);
            return View(viewModel);
        }

        // POST: Accessories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AccessoryViewModel viewModel)
        {
            if (id != viewModel.ProductID)
            {
                return NotFound();
            }
            
            if (ModelState.IsValid)
            {
                try
                {
                    var accessory = await _context.Accessories
                        .Include(a => a.Images)
                        .FirstOrDefaultAsync(a => a.ProductID == id);

                    if (accessory == null) return NotFound();

                    accessory.Name = viewModel.Name;
                    accessory.Description = viewModel.Description;
                    accessory.Price = viewModel.Price;
                    accessory.Cost = viewModel.Cost;
                    accessory.SKU = viewModel.SKU;
                    accessory.CategoryID = viewModel.CategoryID;
                    accessory.BrandID = viewModel.BrandID;
                    accessory.Color = viewModel.Color;
                    accessory.IsActive = viewModel.IsActive;

                    if (viewModel.NewImageFile != null)
                    {
                        var imageUrl = await _fileStorageService.SaveFileAsync(viewModel.NewImageFile, _containerName);
                        accessory.Images.Add(new ProductImage
                        {
                            ImageUrl = imageUrl,
                            IsPrimary = !accessory.Images.Any()
                        });
                    }

                    _context.Update(accessory);
                    await _context.SaveChangesAsync();
                    await _auditService.LogAsync("Accessories", $"Editó el accesorio '{accessory.Name}' (ID: {accessory.ProductID}).");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AccessoryExists(viewModel.ProductID))
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
            await PopulateDropdowns(viewModel.CategoryID, viewModel.BrandID);
            return View(viewModel);
        }

        // GET: Accessories/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var accessory = await _context.Accessories
                .Include(a => a.Category)
                .Include(a => a.Images)
                .Include(a => a.Brand)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ProductID == id);
            if (accessory == null)
            {
                return NotFound();
            }

            return View(accessory);
        }

        // POST: Accessories/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var accessory = await _context.Accessories.Include(a => a.Images).FirstOrDefaultAsync(a => a.ProductID == id);
            if (accessory != null)
            {
                accessory.IsActive = false; // Borrado lógico
                _context.Update(accessory);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Accessories", $"Eliminó el accesorio '{accessory.Name}' (ID: {accessory.ProductID}).");
            }
            
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(int imageId, int productId)
        {
            var image = await _context.ProductImages.FindAsync(imageId);
            if (image == null) return NotFound();

            await _fileStorageService.DeleteFileAsync(image.ImageUrl);
            _context.ProductImages.Remove(image);

            if (image.IsPrimary)
            {
                var nextImage = await _context.ProductImages
                    .Where(i => i.ProductID == productId && i.ProductImageID != imageId)
                    .FirstOrDefaultAsync();
                if (nextImage != null)
                {
                    nextImage.IsPrimary = true;
                }
            }

            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Accessories", $"Eliminó una imagen del producto con ID {productId}.");

            return RedirectToAction(nameof(Edit), new { id = productId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPrimaryImage(int imageId, int productId)
        {
            var images = await _context.ProductImages.Where(i => i.ProductID == productId).ToListAsync();
            foreach (var img in images)
            {
                img.IsPrimary = (img.ProductImageID == imageId);
            }
            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Accessories", $"Estableció una nueva imagen principal para el producto con ID {productId}.");
            return RedirectToAction(nameof(Edit), new { id = productId });
        }

        private bool AccessoryExists(int id)
        {
            return _context.Accessories.Any(e => e.ProductID == id);
        }

        private async Task PopulateDropdowns(int? categoryId = null, int? brandId = null)
        {
            var activeCategories = await _context.Categories.Where(c => c.IsActive).AsNoTracking().OrderBy(c => c.Name).ToListAsync();
            var activeBrands = await _context.Brands.Where(b => b.IsActive).AsNoTracking().OrderBy(b => b.Name).ToListAsync();

            ViewData["CategoryID"] = new SelectList(activeCategories, "CategoryID", "Name", categoryId);
            ViewData["BrandID"] = new SelectList(activeBrands, "BrandID", "Name", brandId);
        }
    }
}
