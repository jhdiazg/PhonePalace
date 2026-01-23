﻿﻿﻿﻿﻿using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Web.ViewModels;
using PhonePalace.Infrastructure.Data;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class ModelsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public ModelsController(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        // GET: Models
        public async Task<IActionResult> Index()
        {
            var models = await _context.Models
                .Where(m => m.IsActive)
                .Include(m => m.Brand)
                .AsNoTracking()
                .Select(m => new ModelViewModel
                {
                    ModelID = m.ModelID,
                    Name = m.Name,
                    BrandName = m.Brand.Name
                })
                .ToListAsync();
            return View(models);
        }

        // GET: Models/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var model = await _context.Models
                .Include(m => m.Brand)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ModelID == id);
            if (model == null)
            {
                return NotFound();
            }
            
            var viewModel = new ModelViewModel
            {
                ModelID = model.ModelID,
                Name = model.Name,
                BrandName = model.Brand.Name
            };
            return View(viewModel);
        }

        // GET: Models/Create
        public async Task<IActionResult> Create()
        {
            await PopulateBrandsDropdown();
            return View();
        }

        // POST: Models/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ModelViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var model = new Model
                {
                    Name = viewModel.Name.ToUpper(),
                    BrandID = viewModel.BrandID
                };
                _context.Add(model);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Modelos", $"Creó el modelo '{model.Name}' (ID: {model.ModelID}).");
                return RedirectToAction(nameof(Index));
            }
            await PopulateBrandsDropdown(viewModel.BrandID);
            return View(viewModel);
        }

        // GET: Models/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var model = await _context.Models.FindAsync(id);
            if (model == null)
            {
                return NotFound();
            }

            var viewModel = new ModelViewModel
            {
                ModelID = model.ModelID,
                Name = model.Name,
                BrandID = model.BrandID,
                IsActive = model.IsActive
            };

            await PopulateBrandsDropdown(viewModel.BrandID);
            return View(viewModel);
        }

        // POST: Models/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ModelViewModel viewModel)
        {
            if (id != viewModel.ModelID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var modelToUpdate = await _context.Models.FindAsync(id);
                    if (modelToUpdate == null) return NotFound();

                    modelToUpdate.Name = viewModel.Name.ToUpper();
                    modelToUpdate.BrandID = viewModel.BrandID;
                    modelToUpdate.IsActive = viewModel.IsActive;

                    _context.Update(modelToUpdate);
                    await _context.SaveChangesAsync();
                    await _auditService.LogAsync("Modelos", $"Editó el modelo '{modelToUpdate.Name}' (ID: {modelToUpdate.ModelID}).");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ModelExists(viewModel.ModelID))
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
            await PopulateBrandsDropdown(viewModel.BrandID);
            return View(viewModel);
        }

        // GET: Models/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var model = await _context.Models
                .Include(m => m.Brand)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ModelID == id);
            if (model == null)
            {
                return NotFound();
            }

            return View(model);
        }

        // POST: Models/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var model = await _context.Models.FindAsync(id);
            if (model != null)
            {
                model.IsActive = false;
                _context.Update(model);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Modelos", $"Eliminó el modelo '{model.Name}' (ID: {model.ModelID}).");
            }
            
            return RedirectToAction(nameof(Index));
        }

        private bool ModelExists(int id)
        {
            return _context.Models.Any(e => e.ModelID == id);
        }

        private async Task PopulateBrandsDropdown(object? selectedBrand = null)
        {
            var brandsList = await _context.Brands.Where(b => b.IsActive).AsNoTracking().OrderBy(b => b.Name).ToListAsync();
            ViewData["BrandID"] = new SelectList(brandsList, "BrandID", "Name", selectedBrand);
        }
    }
}