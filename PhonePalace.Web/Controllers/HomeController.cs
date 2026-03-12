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
using Microsoft.AspNetCore.Identity.UI.Services;
using PhonePalace.Domain.Entities;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Administrador,Vendedor,Almacenista,Cajero,Contador")] // Requiere que el usuario inicie sesión para ver el dashboard
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;
        private readonly IEmailSender _emailSender;
        private readonly ICashService _cashService;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, ICashService cashService, IEmailSender emailSender)
        {
            _logger = logger;
            _context = context;
            _emailSender = emailSender;
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

            var invoicesQuery = _context.Invoices
                .Where(i => i.SaleDate.Month == DateTime.Now.Month &&
                            i.SaleDate.Year == DateTime.Now.Year &&
                            i.Status == InvoiceStatus.Completed);

            if (User.IsInRole("Contador"))
            {
                // Para el contador, solo ventas con IVA.
                invoicesQuery = invoicesQuery.Where(i => _context.Set<ElectronicInvoice>().Any(e => e.InvoiceID == i.InvoiceID && e.Status == "Accepted"));
            }

            var currentMonthSales = await invoicesQuery.SumAsync(i => i.Total);

            // --- NUEVO: Desglose Facturación Electrónica vs Local ---
            var electronicSales = await _context.Set<ElectronicInvoice>()
                .Include(e => e.Invoice)
                .Where(e => e.Invoice.SaleDate.Month == DateTime.Now.Month &&
                            e.Invoice.SaleDate.Year == DateTime.Now.Year &&
                            e.Invoice.Status == InvoiceStatus.Completed &&
                            e.Status == "Accepted")
                .SumAsync(e => e.Invoice.Total);

            ViewBag.ElectronicSales = electronicSales;
            ViewBag.LocalSales = currentMonthSales - electronicSales;

            // Restar devoluciones realizadas en el mes actual para obtener Ventas Netas
            var currentMonthReturns = await _context.Returns
                .Where(r => r.Date.Month == DateTime.Now.Month &&
                            r.Date.Year == DateTime.Now.Year)
                .SumAsync(r => r.TotalAmount);

            // --- INICIO: Añadir Otros Ingresos del mes (que no son ventas) ---
            var today = DateTime.Now;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);

            // Ingresos de Caja que no son abonos a cartera
            var otherCashIncome = await _context.CashMovements
                .Where(cm => cm.MovementDate >= startOfMonth && cm.MovementDate <= endOfMonth &&
                             cm.MovementType == CashMovementType.Income &&
                             cm.Description!.ToUpper().Contains("ABONO A CXC") &&
                             cm.Description.ToUpper().Contains("POR VENTA"))
                .SumAsync(cm => cm.Amount);

            // Ingresos de Banco que no son de ventas, abonos o transferencias
            var otherBankIncome = await _context.BankTransactions
                .Where(bt => bt.Date >= startOfMonth && bt.Date <= endOfMonth &&
                             bt.Amount > 0 && // Ingresos
                             !bt.Description!.ToUpper().Contains("INGRESO POR VENTA") &&
                             !bt.Description.ToUpper().Contains("ABONO CXC") &&
                             !bt.Description.ToUpper().Contains("TRANSFERENCIA") &&
                             !bt.Description.ToUpper().Contains("RETIRO"))
                .SumAsync(bt => bt.Amount);

            // Ingresos devengados (CxC tipo Otro) del mes
            var otherReceivablesIncome = await _context.AccountReceivables
                .Where(ar => ar.Date >= startOfMonth && ar.Date <= endOfMonth && ar.Type == "Otro")
                .SumAsync(ar => ar.TotalAmount);

            currentMonthSales += otherCashIncome + otherBankIncome + otherReceivablesIncome;
            // --- FIN: Añadir Otros Ingresos del mes ---

            currentMonthSales -= currentMonthReturns;

            // Obtener saldos actuales de Caja y Bancos
            var cashBalance = await _cashService.GetCurrentBalanceAsync();
            // Aseguramos que sea la consulta simple
            var banksBalance = await _context.Banks.Where(b => b.IsActive).SumAsync(b => b.Balance);

            var arQuery = _context.AccountReceivables.Where(ar => !ar.IsPaid);
            if (User.IsInRole("Contador"))
            {
                var saleIdsWithVat = _context.Sales
                    .Where(s => _context.Set<ElectronicInvoice>().Any(ei => ei.InvoiceID == s.InvoiceID && ei.Status == "Accepted"))
                    .Select(s => s.SaleID);
                arQuery = arQuery.Where(ar => ar.SaleID.HasValue && saleIdsWithVat.Contains(ar.SaleID.Value));
            }
            var totalReceivables = await arQuery.SumAsync(ar => ar.Balance);

            var apQuery = _context.AccountPayables.Where(ap => !ap.IsPaid);
            if (User.IsInRole("Contador"))
            {
                var purchaseIdsWithVat = _context.Purchases
                    .Where(p => p.PurchaseDetails.Any(pd => pd.TaxRate > 0))
                    .Select(p => p.Id);
                apQuery = apQuery.Where(ap => ap.PurchaseId.HasValue && purchaseIdsWithVat.Contains(ap.PurchaseId.Value));
            }
            var totalPayables = await apQuery.SumAsync(ap => ap.Balance);

            // --- Lógica para Gráfica de Márgenes (Mes Actual) ---
            // Obtenemos los detalles de venta del mes actual
            var salesDetailsQuery = _context.Set<Domain.Entities.SaleDetail>()
                .Include(d => d.Product)
                .Include(d => d.Sale)
                .Where(d => d.Sale.SaleDate.Month == DateTime.Now.Month && 
                            d.Sale.SaleDate.Year == DateTime.Now.Year &&
                            !d.Sale.IsDeleted);

            if (User.IsInRole("Contador"))
            {
                salesDetailsQuery = salesDetailsQuery.Where(d => _context.Set<ElectronicInvoice>().Any(e => e.InvoiceID == d.Sale.InvoiceID && e.Status == "Accepted"));
            }

            var salesDetails = await salesDetailsQuery.ToListAsync();

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

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            TempData["Error"] = "No tiene los permisos necesarios para acceder a este recurso.";
            return RedirectToAction(nameof(Index));
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