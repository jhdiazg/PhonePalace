using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.ViewModels;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System;
using PhonePalace.Domain.Enums;
using PhonePalace.Domain.Interfaces;

namespace PhonePalace.Web.Controllers
{
    [Authorize] // Requiere que el usuario inicie sesión para ver el dashboard
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;
        private readonly ICashService _cashService;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, ICashService cashService)
        {
            _logger = logger;
            _context = context;
            _cashService = cashService;
        }

        public async Task<IActionResult> Index()
        {
            // Umbral de bajo stock para los productos.
            const int lowStockThreshold = 5;

            var totalInventoryValue = await _context.Inventories
                .SumAsync(i => i.Product.Cost * (decimal)((double)i.Stock));

            // Obtenemos los productos con bajo stock.
            // Usamos Sum() porque un producto puede tener múltiples registros de inventario.
            var lowStockProducts = await _context.Products
                .Where(p => p.IsActive && p.InventoryLevels.Any())
                .Select(p => new {
                    p.Name,
                    CurrentStock = (int)p.InventoryLevels.Sum(i => (double)i.Stock)
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

            var currentMonthSales = await _context.Invoices
                .Where(i => i.SaleDate.Month == DateTime.Now.Month &&
                            i.SaleDate.Year == DateTime.Now.Year &&
                            i.Status == InvoiceStatus.Completed)
                .SumAsync(i => i.Total);

            // Obtener saldos actuales de Caja y Bancos
            var cashBalance = await _cashService.GetCurrentBalanceAsync();
            var banksBalance = await _context.Banks.Where(b => b.IsActive).SumAsync(b => b.Balance);
            var totalReceivables = await _context.AccountReceivables.Where(ar => !ar.IsPaid).SumAsync(ar => ar.Balance);

            // --- Lógica para Gráfica de Márgenes (Mes Actual) ---
            // Obtenemos los detalles de venta del mes actual
            var salesDetails = await _context.Set<Domain.Entities.SaleDetail>()
                .Include(d => d.Product)
                .Include(d => d.Sale)
                .Where(d => d.Sale.SaleDate.Month == DateTime.Now.Month && 
                            d.Sale.SaleDate.Year == DateTime.Now.Year &&
                            !d.Sale.IsDeleted)
                .ToListAsync();

            // Definimos los contadores para los rangos
            int countNegative = 0; // Pérdida
            int countLow = 0;      // 0% - 15%
            int countMedium = 0;   // 15% - 30%
            int countHigh = 0;     // 30% - 50%
            int countVeryHigh = 0; // > 50%

            foreach (var item in salesDetails)
            {
                if (item.UnitPrice == 0) continue;

                // Margen Bruto = (Precio Venta - Costo) / Precio Venta
                // Nota: Usamos el costo actual del producto. Idealmente debería ser el costo histórico.
                decimal margin = (item.UnitPrice - item.Product.Cost) / item.UnitPrice * 100m;

                if (margin < 0) countNegative += item.Quantity;
                else if (margin < 15) countLow += item.Quantity;
                else if (margin < 30) countMedium += item.Quantity;
                else if (margin < 50) countHigh += item.Quantity;
                else countVeryHigh += item.Quantity;
            }

            var salesByMargin = new List<SalesByMarginViewModel>
            {
                new SalesByMarginViewModel { RangeLabel = "Pérdida (<0%)", Quantity = countNegative },
                new SalesByMarginViewModel { RangeLabel = "Bajo (0-15%)", Quantity = countLow },
                new SalesByMarginViewModel { RangeLabel = "Medio (15-30%)", Quantity = countMedium },
                new SalesByMarginViewModel { RangeLabel = "Alto (30-50%)", Quantity = countHigh },
                new SalesByMarginViewModel { RangeLabel = "Muy Alto (>50%)", Quantity = countVeryHigh }
            };
            // ----------------------------------------------------

            var viewModel = new DashboardViewModel
            {
                TotalClients = await _context.Clients.CountAsync(c => c.IsActive),
                TotalProducts = await _context.Products.CountAsync(p => p.IsActive),
                TotalInventoryValue = totalInventoryValue,
                CurrentMonthSales = currentMonthSales,
                CashBalance = cashBalance,
                BanksBalance = banksBalance,
                TotalAccountsReceivable = totalReceivables,
                LowStockProducts = lowStockProducts,
                SalesByMargin = salesByMargin
            };

            return View(viewModel);
        }

        // En HomeController.cs
        [AllowAnonymous] // <--- AGREGAR ESTO
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}