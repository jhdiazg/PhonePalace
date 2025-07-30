using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.ViewModels;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace PhonePalace.Web.Controllers
{
    [Authorize] // Requiere que el usuario inicie sesión para ver el dashboard
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            const int lowStockThreshold = 5;

            var totalInventoryValue = await _context.Inventories
                .SumAsync(i => i.Product.Cost * i.Stock);

            // Obtenemos los productos con bajo stock.
            // Usamos Sum() porque un producto puede tener múltiples registros de inventario.
            var lowStockProducts = await _context.Products
                .Where(p => p.IsActive && p.InventoryLevels.Any())
                .Select(p => new {
                    p.Name,
                    CurrentStock = p.InventoryLevels.Sum(i => i.Stock)
                })
                .Where(p => p.CurrentStock <= lowStockThreshold)
                .OrderBy(p => p.CurrentStock)
                .Take(10)
                .Select(p => new LowStockProductViewModel {
                    ProductName = p.Name,
                    CurrentStock = p.CurrentStock,
                    ReorderLevel = lowStockThreshold
                })
                .ToListAsync();

            var viewModel = new DashboardViewModel
            {
                TotalClients = await _context.Clients.CountAsync(c => c.IsActive),
                TotalProducts = await _context.Products.CountAsync(p => p.IsActive),
                TotalInventoryValue = totalInventoryValue,
                CurrentMonthSales = 0, // Placeholder: Se calculará cuando exista el módulo de Ventas
                LowStockProducts = lowStockProducts
            };

            return View(viewModel);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}