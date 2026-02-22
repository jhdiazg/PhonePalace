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
            // Si el usuario es un Cliente, redirigirlo a su portal y no mostrar el dashboard
            if (User.IsInRole("Cliente"))
            {
                return RedirectToAction("MyData", "ClientPortal");
            }

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

            // Restar devoluciones realizadas en el mes actual para obtener Ventas Netas
            var currentMonthReturns = await _context.Returns
                .Where(r => r.Date.Month == DateTime.Now.Month &&
                            r.Date.Year == DateTime.Now.Year)
                .SumAsync(r => r.TotalAmount);
            currentMonthSales -= currentMonthReturns;

            // Obtener saldos actuales de Caja y Bancos
            var cashBalance = await _cashService.GetCurrentBalanceAsync();
            var banksBalance = await _context.Banks.Where(b => b.IsActive).SumAsync(b => b.Balance);
            var totalReceivables = await _context.AccountReceivables.Where(ar => !ar.IsPaid).SumAsync(ar => ar.Balance);
            var totalPayables = await _context.AccountPayables.Where(ap => !ap.IsPaid).SumAsync(ap => ap.Amount);

            // --- Lógica para Gráfica de Márgenes (Mes Actual) ---
            // Obtenemos los detalles de venta del mes actual
            var salesDetails = await _context.Set<Domain.Entities.SaleDetail>()
                .Include(d => d.Product)
                .Include(d => d.Sale)
                .Where(d => d.Sale.SaleDate.Month == DateTime.Now.Month && 
                            d.Sale.SaleDate.Year == DateTime.Now.Year &&
                            !d.Sale.IsDeleted)
                .ToListAsync();

            // Obtener devoluciones asociadas a estas ventas para calcular cantidad neta real
            var saleIds = salesDetails.Select(s => s.SaleID).Distinct().ToList();
            var returnDetails = await _context.ReturnDetails
                .Include(rd => rd.Return)
                .Where(rd => saleIds.Contains(rd.Return.SaleID))
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

                // Calcular cantidad neta (Venta - Devolución)
                var returnedQty = returnDetails
                    .Where(rd => rd.Return.SaleID == item.SaleID && rd.ProductID == item.ProductID)
                    .Sum(rd => rd.Quantity);
                
                var netQuantity = item.Quantity - returnedQty;
                if (netQuantity <= 0) continue; // Si se devolvió todo, no contar

                // Margen Bruto = (Precio Venta - Costo) / Precio Venta
                // Usamos costo histórico si está disponible
                decimal cost = item.Cost > 0 ? item.Cost : item.Product.Cost;
                decimal margin = (item.UnitPrice - cost) / item.UnitPrice * 100m;

                if (margin < 0) countNegative += netQuantity;
                else if (margin < 15) countLow += netQuantity;
                else if (margin < 30) countMedium += netQuantity;
                else if (margin < 50) countHigh += netQuantity;
                else countVeryHigh += netQuantity;
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
                TotalAccountsPayable = totalPayables,
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