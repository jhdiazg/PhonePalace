﻿﻿﻿﻿﻿﻿﻿using PhonePalace.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
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
using PhonePalace.Domain.Enums;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class CellPhonesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IFileStorageService _fileStorageService;
        private readonly string _containerName = "products";

        public CellPhonesController(ApplicationDbContext context, IAuditService auditService, IFileStorageService fileStorageService)
        {
            _context = context;
            _auditService = auditService;
            _fileStorageService = fileStorageService;
        }

        // GET: CellPhones
        public async Task<IActionResult> Index(int? pageNumber, int? pageSize)
        {
            ViewData["PageSize"] = pageSize ?? 10;

            var cellPhones = _context.CellPhones
                .Include(c => c.Images)
                .Include(c => c.Category)
                .Include(c => c.Model)
                .ThenInclude(m => m.Brand)
                .AsNoTracking();

            return View(await PaginatedList<CellPhone>.CreateAsync(cellPhones, pageNumber ?? 1, pageSize ?? 10));
        }

        // GET: CellPhones/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cellPhone = await _context.CellPhones
                .Include(c => c.Category)
                .Include(c => c.Images)
                .Include(c => c.Model)
                .ThenInclude(m => m.Brand)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ProductID == id);
            if (cellPhone == null)
            {
                return NotFound();
            }

            return View(cellPhone);
        }

        // GET: CellPhones/Create
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View(new CellPhoneViewModel());
        }

        // POST: CellPhones/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CellPhoneViewModel viewModel)
        {
            if (!string.IsNullOrEmpty(viewModel.SKU))
            {
                if (await _context.Products.AnyAsync(p => p.SKU == viewModel.SKU.ToUpper()))
                {
                    ModelState.AddModelError("SKU", "Este SKU ya está registrado en otro producto.");
                }
            }

            if (ModelState.IsValid)
            {
                var cellPhone = new CellPhone
                {
                    Name = viewModel.Name.ToUpper(),
                    Description = viewModel.Description?.ToUpper(),
                    Price = viewModel.Price,
                    Cost = viewModel.Cost,
                    SKU = viewModel.SKU?.ToUpper(),
                    CategoryID = viewModel.CategoryID,
                    ModelID = viewModel.ModelID,
                    Color = viewModel.Color?.ToUpper(),
                    StorageGB = viewModel.StorageGB,
                    RamGB = viewModel.RamGB,
                    IsActive = viewModel.IsActive
                };

                if (viewModel.NewImageFile != null)
                {
                    var imageUrl = await _fileStorageService.SaveFileAsync(viewModel.NewImageFile, _containerName);
                    cellPhone.Images.Add(new ProductImage
                    {
                        ImageUrl = imageUrl,
                        IsPrimary = true
                    });
                }

                _context.Add(cellPhone);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Celulares", $"Creó el celular '{cellPhone.Name}' (ID: {cellPhone.ProductID}).");
                return RedirectToAction(nameof(Index));
            }
            await PopulateDropdowns(viewModel.CategoryID, viewModel.ModelID, null, (ProductCondition?)viewModel.ProductCondition);
            return View(viewModel);
        }

        // GET: CellPhones/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cellPhone = await _context.CellPhones
                .Include(c => c.Images)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ProductID == id);

            if (cellPhone == null)
            {
                return NotFound();
            }

            var viewModel = new CellPhoneViewModel
            {
                ProductID = cellPhone.ProductID,
                Name = cellPhone.Name,
                Description = cellPhone.Description ?? string.Empty,
                Price = cellPhone.Price,
                Cost = cellPhone.Cost,
                SKU = cellPhone.SKU ?? string.Empty,
                CategoryID = cellPhone.CategoryID,
                ModelID = cellPhone.ModelID,
                Color = cellPhone.Color ?? string.Empty,
                StorageGB = cellPhone.StorageGB,
                RamGB = cellPhone.RamGB,
                IsActive = cellPhone.IsActive,
                ProductCondition = cellPhone.ProductCondition == 0 ? ProductCondition.Nuevo : cellPhone.ProductCondition,
                Images = cellPhone.Images.Select(i => new ProductImageViewModel
                {
                    ProductImageID = i.ProductImageID,
                    ImageUrl = i.ImageUrl,
                    IsPrimary = i.IsPrimary
                }).ToList()
            };

            var model = await _context.Models.FindAsync(viewModel.ModelID);
            await PopulateDropdowns(viewModel.CategoryID, viewModel.ModelID, model?.BrandID, viewModel.ProductCondition);
            return View(viewModel);
        }

        // POST: CellPhones/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CellPhoneViewModel viewModel)
        {
            if (id != viewModel.ProductID)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(viewModel.SKU))
            {
                if (await _context.Products.AnyAsync(p => p.SKU == viewModel.SKU.ToUpper() && p.ProductID != id))
                {
                    ModelState.AddModelError("SKU", "Este SKU ya está registrado en otro producto.");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var cellPhone = await _context.CellPhones
                        .Include(c => c.Images)
                        .FirstOrDefaultAsync(c => c.ProductID == id);

                    if (cellPhone == null) return NotFound();

                    // Mapear propiedades
                    cellPhone.Name = viewModel.Name.ToUpper();
                    cellPhone.Description = viewModel.Description?.ToUpper();
                    cellPhone.Price = viewModel.Price;
                    cellPhone.Cost = viewModel.Cost;
                    cellPhone.SKU = viewModel.SKU?.ToUpper();
                    cellPhone.CategoryID = viewModel.CategoryID;
                    cellPhone.ModelID = viewModel.ModelID;
                    cellPhone.Color = viewModel.Color?.ToUpper()!;
                    cellPhone.StorageGB = viewModel.StorageGB;
                    cellPhone.RamGB = viewModel.RamGB;
                    cellPhone.IsActive = viewModel.IsActive;
                    cellPhone.ProductCondition = viewModel.ProductCondition;

                    // Manejar nueva imagen
                    if (viewModel.NewImageFile != null)
                    {
                        var imageUrl = await _fileStorageService.SaveFileAsync(viewModel.NewImageFile, _containerName);
                        cellPhone.Images.Add(new ProductImage
                        {
                            ImageUrl = imageUrl,
                            // Si no hay imágenes, la nueva es la principal
                            IsPrimary = !cellPhone.Images.Any()
                        });
                    }

                    _context.Update(cellPhone);
                    await _context.SaveChangesAsync();
                    await _auditService.LogAsync("Celulares", $"Editó el celular '{cellPhone.Name}' (ID: {cellPhone.ProductID}).");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CellPhoneExists(viewModel.ProductID))
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
            var model = await _context.Models.FindAsync(viewModel.ModelID);
            await PopulateDropdowns(viewModel.CategoryID, viewModel.ModelID, model?.BrandID, viewModel.ProductCondition);
            return View(viewModel);
        }

        // GET: CellPhones/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cellPhone = await _context.CellPhones
                .Include(c => c.Category)
                .Include(c => c.Images)
                .Include(c => c.Model)
                .ThenInclude(m => m.Brand)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ProductID == id);
            if (cellPhone == null)
            {
                return NotFound();
            }

            return View(cellPhone);
        }

        // POST: CellPhones/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var cellPhone = await _context.CellPhones.Include(p => p.Images).FirstOrDefaultAsync(p => p.ProductID == id);
            if (cellPhone != null)
            {
                cellPhone.IsActive = false; 
                _context.Update(cellPhone);
                
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Celulares", $"Eliminó el celular '{cellPhone.Name}' (ID: {cellPhone.ProductID}).");
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
            await _auditService.LogAsync("Celulares", $"Eliminó una imagen del producto con ID {productId}.");

            return RedirectToAction(nameof(Edit), new { id = productId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPrimaryImage(int imageId, int productId)
        {
            var images = await _context.ProductImages.Where(i => i.ProductID == productId).ToListAsync();
            if (!images.Any(i => i.ProductImageID == imageId))
            {
                return Json(new { success = false, message = "Imagen no encontrada para este producto." });
            }

            foreach (var img in images)
            {
                img.IsPrimary = (img.ProductImageID == imageId);
            }
            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Celulares", $"Estableció una nueva imagen principal para el producto con ID {productId}.");

            var updatedImages = await GetImagesForProduct(productId);
            ViewData["ProductID"] = productId;
            return PartialView("_ImageGalleryPartial", updatedImages);
        }

        private bool CellPhoneExists(int id)
        {
            return _context.CellPhones.Any(e => e.ProductID == id);
        }

        private async Task PopulateDropdowns(int? categoryId = null, int? modelId = null, int? brandId = null, ProductCondition? condition = null)
        {
            ViewData["CategoryID"] = new SelectList(await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).AsNoTracking().ToListAsync(), "CategoryID", "Name", categoryId);
            ViewData["BrandID"] = new SelectList(await _context.Brands.Where(b => b.IsActive).OrderBy(b => b.Name).AsNoTracking().ToListAsync(), "BrandID", "Name", brandId);

            var modelsQuery = _context.Models.Where(m => m.IsActive).OrderBy(m => m.Name).AsNoTracking();
            if (brandId.HasValue)
            {
                modelsQuery = modelsQuery.Where(m => m.BrandID == brandId.Value);
            }
            ViewData["ModelID"] = new SelectList(await modelsQuery.ToListAsync(), "ModelID", "Name", modelId);
            ViewData["ProductCondition"] = new SelectList(Enum.GetValues(typeof(ProductCondition)), condition);
        }

        public async Task<JsonResult> GetModelsByBrand(int brandId)
        {
            var models = await _context.Models
                .Where(m => m.BrandID == brandId && m.IsActive)
                .OrderBy(m => m.Name)
                .Select(m => new { value = m.ModelID, text = m.Name })
                .ToListAsync();
            return Json(models);
        }

        private async Task<List<ProductImageViewModel>> GetImagesForProduct(int productId)
        {
            return await _context.ProductImages
                .Where(i => i.ProductID == productId)
                .AsNoTracking()
                .Select(i => new ProductImageViewModel
                {
                    ProductImageID = i.ProductImageID,
                    ImageUrl = i.ImageUrl,
                    IsPrimary = i.IsPrimary
                }).ToListAsync();
        }
    }
}
