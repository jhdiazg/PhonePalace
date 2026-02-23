using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        // GET: /Products (MVC View)
        public async Task<IActionResult> Index(string sortOrder, string currentFilter, string searchString, int? categoryId, int? pageNumber, int? pageSize)
        {
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["PriceSortParm"] = sortOrder == "Price" ? "price_desc" : "Price";
            ViewData["TypeSortParm"] = sortOrder == "Type" ? "type_desc" : "Type";
            ViewData["BrandSortParm"] = sortOrder == "Brand" ? "brand_desc" : "Brand";
            ViewData["PageSize"] = pageSize ?? 10;
            ViewData["CategoryID"] = new SelectList(_context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).AsNoTracking(), "CategoryID", "Name", categoryId);
            ViewData["CurrentCategory"] = categoryId;

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
                .AsQueryable(); // Start with IQueryable

            if (categoryId.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.CategoryID == categoryId);
            }

            var cellPhonesQuery = productsQuery.OfType<CellPhone>()
                .Include(cp => cp.Model)
                    .ThenInclude(m => m.Brand)
                .Select(cp => new ProductIndexViewModel
                {
                    ProductID = cp.ProductID,
                    SKU = cp.SKU,
                    Code = cp.Code,
                    Name = cp.Name,
                    ProductType = "Celular", // Directly assign for CellPhone
                    CategoryName = cp.Category != null ? cp.Category.Name : "N/A",
                    BrandName = cp.Model != null && cp.Model.Brand != null ? cp.Model.Brand.Name : "N/A",
                    ModelName = cp.Model != null ? cp.Model.Name : null,
                    Price = cp.Price,
                    Cost = cp.Cost,
                    IsActive = cp.IsActive,
                    PrimaryImageUrl = cp.Images.OrderByDescending(i => i.IsPrimary).Select(i => i.ImageUrl).FirstOrDefault()
                });

            var accessoriesQuery = productsQuery.OfType<Accessory>()
                .Include(a => a.Brand)
                .Select(a => new ProductIndexViewModel
                {
                    ProductID = a.ProductID,
                    SKU = a.SKU,
                    Code = a.Code,
                    Name = a.Name,
                    ProductType = "Accesorio", // Directly assign for Accessory
                    CategoryName = a.Category != null ? a.Category.Name : "N/A",
                    BrandName = a.Brand != null ? a.Brand.Name : "N/A",
                    ModelName = null, // Accessories don't have models
                    Price = a.Price,
                    Cost = a.Cost,
                    IsActive = a.IsActive,
                    PrimaryImageUrl = a.Images.OrderByDescending(i => i.IsPrimary).Select(i => i.ImageUrl).FirstOrDefault()
                });

            // Union the two queries
            var combinedQuery = cellPhonesQuery.Union(accessoriesQuery);

            // Apply search filter
            if (!string.IsNullOrEmpty(searchString))
            {
                combinedQuery = combinedQuery.Where(p => p.Name.Contains(searchString)
                                       || (p.SKU != null && p.SKU.Contains(searchString))
                                       || (p.Code != null && p.Code.Contains(searchString))
                                       || (!string.IsNullOrEmpty(p.BrandName) && p.BrandName.Contains(searchString))
                                       || (p.ModelName != null && p.ModelName.Contains(searchString))
                                       || p.ProductType.Contains(searchString));
            }

            // Apply sorting
            combinedQuery = sortOrder switch
            {
                "name_desc" => combinedQuery.OrderByDescending(p => p.Name),
                "Price" => combinedQuery.OrderBy(p => p.Price),
                "price_desc" => combinedQuery.OrderByDescending(p => p.Price),
                "Type" => combinedQuery.OrderBy(p => p.ProductType),
                "type_desc" => combinedQuery.OrderByDescending(p => p.ProductType),
                "Brand" => combinedQuery.OrderBy(p => p.BrandName),
                "brand_desc" => combinedQuery.OrderByDescending(p => p.BrandName),
                _ => combinedQuery.OrderBy(p => p.Name),
            };

            var paginatedProducts = await PaginatedList<ProductIndexViewModel>.CreateAsync(combinedQuery.AsNoTracking(), pageNumber ?? 1, pageSize ?? 10);

            return View(paginatedProducts);
        }

        // API Endpoint for product search (used by Purchase Create view)
        [HttpGet("api/products")]
        public async Task<IActionResult> SearchProductsApi([FromQuery] string term, [FromQuery] int? categoryId)
        {
            var query = _context.Products.Where(p => p.IsActive);

            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryID == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(term) && term.Length >= 1)
            {
                string searchTerm = term.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(searchTerm) ||
                                         (p.SKU != null && p.SKU.ToLower().Contains(searchTerm)) ||
                                         (p.Code != null && p.Code.ToLower().Contains(searchTerm)) ||
                                         (p is CellPhone && (
                                             ((CellPhone)p).Model.Name.ToLower().Contains(searchTerm) ||
                                             ((CellPhone)p).Model.Brand.Name.ToLower().Contains(searchTerm) ||
                                             (((CellPhone)p).Model.Brand.Name + " " + ((CellPhone)p).Model.Name + " " + p.Name).ToLower().Contains(searchTerm)
                                         )) ||
                                         (p is Accessory && (
                                             ((Accessory)p).Brand.Name.ToLower().Contains(searchTerm) ||
                                             (((Accessory)p).Brand.Name + " " + p.Name).ToLower().Contains(searchTerm)
                                         )));
            }

            var products = await query
                .Select(p => new { p.ProductID, p.Name, p.Price })
                .Take(20) // Limit results for performance
                .ToListAsync();

            return Ok(products);
        }

        // API Endpoint for getting a single product (used by some other parts of the app)
        [HttpGet("api/products/{id}")]
        public async Task<IActionResult> GetProductApi(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null || !product.IsActive)
            {
                return NotFound();
            }

            var stock = await _context.Inventories
                                      .Where(i => i.ProductID == id)
                                      .SumAsync(i => (double)i.Stock);

            return Ok(new { id = product.ProductID, name = product.Name, price = product.Price, stock });
        }
    }
}

            
