﻿﻿﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.Helpers;
using PhonePalace.Web.ViewModels;
using System.Linq;
using System.Threading.Tasks;

namespace PhonePalace.Web.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Products
        public async Task<IActionResult> Index(string sortOrder, string currentFilter, string searchString, int? pageNumber)
        {
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["PriceSortParm"] = sortOrder == "Price" ? "price_desc" : "Price";
            ViewData["TypeSortParm"] = sortOrder == "Type" ? "type_desc" : "Type";
            ViewData["BrandSortParm"] = sortOrder == "Brand" ? "brand_desc" : "Brand";

            if (searchString != null)
            {
                pageNumber = 1;
            }
            else
            {
                searchString = currentFilter;
            }

            ViewData["CurrentFilter"] = searchString;

            var productsQuery = _context.Products
                .Include(c => c.Category)
                .Include(c => c.Images)
                .Include(p => ((CellPhone)p).Model.Brand) // Incluimos propiedades de navegación de tipos derivados
                .Include(p => ((Accessory)p).Brand)
                .Select(p => new ProductIndexViewModel
                {
                    ProductID = p.ProductID,
                    SKU = p.SKU,
                    Name = p.Name,
                    ProductType = EF.Property<string>(p, "ProductType") == "CellPhone" ? "Celular" : "Accesorio",
                    CategoryName = p.Category != null ? p.Category.Name : "N/A",
                    BrandName = (p is CellPhone) ? ((CellPhone)p).Model.Brand.Name : ((p is Accessory) ? (((Accessory)p).Brand != null ? ((Accessory)p).Brand.Name : "N/A") : "N/A"),
                    ModelName = (p is CellPhone) ? ((CellPhone)p).Model.Name : null,
                    Price = p.Price,
                    Cost = p.Cost,
                    PrimaryImageUrl = p.Images.OrderByDescending(i => i.IsPrimary).Select(i => i.ImageUrl).FirstOrDefault()
                });

            if (!string.IsNullOrEmpty(searchString))
            {
                productsQuery = productsQuery.Where(p => p.Name.Contains(searchString)
                                       || (p.SKU != null && p.SKU.Contains(searchString))
                                                                                || (!string.IsNullOrEmpty(p.BrandName) && p.BrandName.Contains(searchString))
                                                                                || (p.ModelName != null && p.ModelName.Contains(searchString)));
            }

            productsQuery = sortOrder switch
            {
                "name_desc" => productsQuery.OrderByDescending(p => p.Name),
                "Price" => productsQuery.OrderBy(p => p.Price),
                "price_desc" => productsQuery.OrderByDescending(p => p.Price),
                "Type" => productsQuery.OrderBy(p => p.ProductType),
                "type_desc" => productsQuery.OrderByDescending(p => p.ProductType),
                "Brand" => productsQuery.OrderBy(p => p.BrandName),
                "brand_desc" => productsQuery.OrderByDescending(p => p.BrandName),
                _ => productsQuery.OrderBy(p => p.Name),
            };

            int pageSize = 10;
            var paginatedProducts = await PaginatedList<ProductIndexViewModel>.CreateAsync(productsQuery.AsNoTracking(), pageNumber ?? 1, pageSize);

            return View(paginatedProducts);
        }

        [HttpGet("api/products/{id}")]
        public async Task<IActionResult> GetProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound();
            }

            var stock = await _context.Inventories
                                      .Where(i => i.ProductID == id)
                                      .SumAsync(i => i.Stock);

            return Ok(new { price = product.Price, stock });
        }
    }
}