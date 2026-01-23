﻿﻿﻿﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class BrandsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public BrandsController(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        // GET: Brands
        public async Task<IActionResult> Index()
        {
            return View(await _context.Brands.Where(b => b.IsActive).AsNoTracking().ToListAsync());
        }

        // GET: Brands/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var brand = await _context.Brands
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.BrandID == id);
            if (brand == null)
            {
                return NotFound();
            }

            return View(brand);
        }

        // GET: Brands/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Brands/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name")] Brand brand)
        {
            if (ModelState.IsValid)
            {
                brand.Name = (brand.Name ?? string.Empty).ToUpper();
                _context.Add(brand);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Marcas", $"Creó la marca '{brand.Name}' (ID: {brand.BrandID}).");
                return RedirectToAction(nameof(Index));
            }
            return View(brand);
        }

        // GET: Brands/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var brand = await _context.Brands.FindAsync(id);
            if (brand == null)
            {
                return NotFound();
            }
            return View(brand);
        }

        // POST: Brands/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("BrandID,Name")] Brand brand)
        {
            if (id != brand.BrandID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    brand.Name = (brand.Name ?? string.Empty).ToUpper();
                    _context.Update(brand);
                    await _context.SaveChangesAsync();
                    await _auditService.LogAsync("Marcas", $"Editó la marca '{brand.Name}' (ID: {brand.BrandID}).");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BrandExists(brand.BrandID))
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
            return View(brand);
        }

        // GET: Brands/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var brand = await _context.Brands
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.BrandID == id);
            if (brand == null)
            {
                return NotFound();
            }

            return View(brand);
        }

        // POST: Brands/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var brand = await _context.Brands.FindAsync(id);
            if (brand != null)
            {
                brand.IsActive = false;
                _context.Update(brand);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Marcas", $"Eliminó la marca '{brand.Name}' (ID: {brand.BrandID}).");
            }
            
            return RedirectToAction(nameof(Index));
        }

        private bool BrandExists(int id)
        {
            return _context.Brands.Any(e => e.BrandID == id);
        }
    }
}