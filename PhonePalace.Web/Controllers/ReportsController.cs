using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.ViewModels;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using PhonePalace.Web.Helpers;
using PhonePalace.Web.Documents;
using QuestPDF.Fluent;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace PhonePalace.Web.Controllers
{
    [Authorize]
    [Route("Reportes")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ReportsController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet]
        [Route("VentasPorFormaPago")]
        public async Task<IActionResult> SalesByPaymentMethod(DateTime? startDate, DateTime? endDate, string? selectedPaymentMethod, int? pageNumber, int? pageSize)
        {
            // Validaciones de fechas
            bool datesAdjusted = false;

            if (startDate.HasValue && startDate.Value.Date > DateTime.Now.Date)
            {
                startDate = DateTime.Now.Date;
                datesAdjusted = true;
            }
            if (endDate.HasValue && startDate.HasValue && endDate.Value.Date < startDate.Value.Date)
            {
                endDate = startDate;
                datesAdjusted = true;
            }

            if (datesAdjusted)
            {
                TempData["Info"] = "Las fechas han sido ajustadas para cumplir con las validaciones.";
                return RedirectToAction(nameof(SalesByPaymentMethod), new { startDate = startDate?.ToString("yyyy-MM-dd"), endDate = endDate?.ToString("yyyy-MM-dd"), selectedPaymentMethod, pageNumber, pageSize });
            }

            var start = startDate ?? DateTime.Today;
            var end = endDate ?? DateTime.Today;
            var currentPageSize = pageSize ?? 15;
            ViewData["PageSize"] = currentPageSize;

            // Ajustar fecha fin para incluir todo el día (hasta 23:59:59)
            var endFilter = end.Date.AddDays(1).AddTicks(-1);

            // Consultamos los pagos y hacemos join con Ventas para obtener el ID de venta para el link
            var query = from p in _context.Payments
                        join s in _context.Sales on p.InvoiceID equals s.InvoiceID into sales
                        from s in sales.DefaultIfEmpty()
                        where p.PaymentDate >= start && p.PaymentDate <= endFilter
                        select new
                        {
                            Payment = p,
                            Invoice = p.Invoice,
                            Client = p.Invoice!.Client ?? null,
                            SaleId = s != null ? s.SaleID : (int?)null
                        };

            if (!string.IsNullOrEmpty(selectedPaymentMethod))
            {
                if (Enum.TryParse<PaymentMethod>(selectedPaymentMethod, out var paymentMethodEnum))
                {
                    query = query.Where(x => x.Payment.PaymentMethod == paymentMethodEnum);
                }
            }

            // 1. Calcular totales y resumen sobre toda la consulta (eficiente)
            var totalSales = await query.SumAsync(x => x.Payment.Amount);
            
            // Calcular total específico de Saldo a Favor para mostrarlo separado
            var totalCustomerBalance = await query
                .Where(x => x.Payment.PaymentMethod == PaymentMethod.CustomerBalance)
                .SumAsync(x => x.Payment.Amount);

            var summary = await query
                .GroupBy(x => x.Payment.PaymentMethod)
                .Select(g => new PaymentMethodSummary
                {
                    PaymentMethod = EnumHelper.GetDisplayName(g.Key),
                    TotalAmount = g.Sum(x => x.Payment.Amount),
                    Count = g.Count()
                })
                .OrderByDescending(s => s.TotalAmount)
                .ToListAsync();

            // 2. Preparar la consulta de detalles para paginar
            var detailsQuery = query.Select(x => new PaymentDetailDto
            {
                PaymentId = x.Payment.PaymentID,
                Date = x.Payment.PaymentDate,
                InvoiceId = x.Payment.InvoiceID,
                SaleId = x.SaleId,
                ClientName = x.Client!.DisplayName ?? "Cliente General",
                PaymentMethod = EnumHelper.GetDisplayName(x.Payment.PaymentMethod),
                Amount = x.Payment.Amount,
                Reference = x.Payment.ReferenceNumber ?? string.Empty
            }).OrderByDescending(d => d.Date);

            // 3. Crear la lista paginada
            var paginatedDetails = await PaginatedList<PaymentDetailDto>.CreateAsync(detailsQuery, pageNumber ?? 1, currentPageSize);

            var model = new SalesReportViewModel
            {
                StartDate = start,
                EndDate = end,
                SelectedPaymentMethod = selectedPaymentMethod,
                PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>().ToList(),
                TotalSales = totalSales,
                Summary = summary,
                Details = paginatedDetails
            };

            // Pasar datos adicionales a la vista para las tarjetas de resumen
            ViewData["TotalCustomerBalance"] = totalCustomerBalance;
            ViewData["TotalRealIncome"] = totalSales - totalCustomerBalance;

            return View(model);
        }

        [HttpGet]
        [Route("ListaPrecios")]
        public IActionResult PriceList()
        {
            return View(new PriceListReportViewModel());
        }

        [HttpPost]
        [Route("ListaPrecios")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GeneratePriceList(PriceListReportViewModel model, bool showAllProducts)
        {
            // Obtener productos activos (filtrando por stock si no se solicita mostrar todos)
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.InventoryLevels)
                .Where(p => p.IsActive)
                .Select(p => new
                {
                    Product = p,
                    TotalStock = p.InventoryLevels.Sum(i => i.Stock)
                })
                .Where(x => showAllProducts || x.TotalStock > 0)
                .Select(x => new ProductIndexViewModel
                {
                    Name = x.Product.Name,
                    Code = x.Product.Code,
                    SKU = x.Product.SKU,
                    CategoryName = x.Product.Category.Name,
                    Cost = x.Product.Cost,
                    ProductType = "General" // Simplificado para el reporte
                })
                .ToListAsync();

            // Cargar Logo
            string wwwRootPath = _webHostEnvironment.WebRootPath;
            string logoPath = Path.Combine(wwwRootPath, "images", "Logo_fact.png");
            byte[] logoBytes = Array.Empty<byte>();

            if (System.IO.File.Exists(logoPath))
            {
                logoBytes = await System.IO.File.ReadAllBytesAsync(logoPath);
            }

            var document = new PriceListPdfDocument(products, model.Type, logoBytes);
            var pdfBytes = document.GeneratePdf();

            return File(pdfBytes, "application/pdf", $"ListaPrecios_{model.Type}_{DateTime.Now:yyyyMMdd}.pdf");
        }

        [HttpGet]
        [Route("MovimientosBancarios")]
        public async Task<IActionResult> BankTransactions(int? bankId, DateTime? startDate, DateTime? endDate)
        {
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today;

            // Ajustar fecha fin para incluir todo el día
            var endFilter = end.Date.AddDays(1).AddTicks(-1);

            var model = new BankReportViewModel
            {
                StartDate = start,
                EndDate = end,
                BankId = bankId,
                Banks = new SelectList(await _context.Banks.AsNoTracking().ToListAsync(), "BankID", "Name", bankId)
            };

            if (bankId.HasValue)
            {
                model.Transactions = await _context.BankTransactions
                    .Where(t => t.BankID == bankId.Value && t.Date >= start && t.Date <= endFilter)
                    .OrderBy(t => t.Date)
                    .AsNoTracking()
                    .ToListAsync();

                // Calcular saldo anterior buscando la última transacción antes del rango
                var lastTransactionBefore = await _context.BankTransactions
                    .Where(t => t.BankID == bankId.Value && t.Date < start)
                    .OrderByDescending(t => t.Date)
                    .FirstOrDefaultAsync();

                model.PreviousBalance = lastTransactionBefore?.BalanceAfterTransaction ?? 0;
            }

            return View(model);
        }

        [HttpGet]
        [Route("MovimientosCaja")]
        public async Task<IActionResult> CashMovements(DateTime? reportDate)
        {
            var date = reportDate ?? DateTime.Today;

            var model = new CashMovementReportViewModel
            {
                ReportDate = date,
                IsCashRegisterFound = false
            };

            // Buscar la caja que se abrió en la fecha especificada.
            // Asumimos que solo hay una caja por día. Si pueden haber más, se necesitaría un selector.
            var cashRegister = await _context.CashRegisters
                .Include(cr => cr.CashMovements)
                // .ThenInclude(cm => cm.User) // Incluir el usuario para mostrar quién hizo el movimiento (eliminado por error de compilación)
                .AsNoTracking()
                .FirstOrDefaultAsync(cr => cr.OpeningDate.Date == date.Date);

            if (cashRegister != null)
            {
                model.IsCashRegisterFound = true;
                model.OpeningBalance = cashRegister.OpeningAmount;
                model.Movements = cashRegister.CashMovements?.OrderBy(m => m.MovementDate).ToList() ?? new List<CashMovement>();

                // Obtener nombres de usuarios para mostrar en la vista
                var userIds = model.Movements.Where(m => m.UserId != null).Select(m => m.UserId).Distinct().ToList();
                ViewBag.UserNames = await _context.Users
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.UserName);

                // Calcular totales
                model.TotalIncome = model.Movements
                    .Where(m => m.MovementType == CashMovementType.Income || m.MovementType == CashMovementType.Opening)
                    .Sum(m => m.Amount);
                model.TotalExpenses = model.Movements.Where(m => m.MovementType == CashMovementType.Expense).Sum(m => m.Amount);
                model.ExpectedBalance = (cashRegister.OpeningAmount + model.Movements.Where(m => m.MovementType == CashMovementType.Income).Sum(m => m.Amount)) - model.TotalExpenses;
            }

            return View(model);
        }

        [HttpGet]
        [Route("BalanceMensual")]
        public async Task<IActionResult> MonthlyBalance(int? year)
        {
            int reportYear = year ?? DateTime.Now.Year;
            var model = new MonthlyBalanceViewModel { Year = reportYear };

            // Inicializar los 12 meses
            var culture = new System.Globalization.CultureInfo("es-CO");
            for (int i = 1; i <= 12; i++)
            {
                model.Items.Add(new MonthlyBalanceItem
                {
                    Month = i,
                    MonthName = culture.DateTimeFormat.GetMonthName(i)
                });
            }

            // 1. Ventas, IVA Ventas y Costo
            var sales = await _context.Sales
                .Include(s => s.Invoice)
                .Include(s => s.Details)
                .ThenInclude(d => d.Product)
                .Where(s => s.SaleDate.Year == reportYear && !s.IsDeleted)
                .AsNoTracking()
                .ToListAsync();

            // Obtener IDs de facturas que tienen Factura Electrónica aceptada
            var salesInvoiceIds = sales.Select(s => s.Invoice.InvoiceID).ToList();
            var electronicInvoiceIds = await _context.Set<ElectronicInvoice>()
                .Where(e => salesInvoiceIds.Contains(e.InvoiceID) && e.Status == "Accepted")
                .Select(e => e.InvoiceID)
                .ToListAsync();
            var electronicInvoiceSet = new HashSet<int>(electronicInvoiceIds);

            foreach (var sale in sales)
            {
                var item = model.Items.First(m => m.Month == sale.SaleDate.Month);
                item.Sales += sale.Invoice.Subtotal;
                
                // Solo sumar IVA si tiene factura electrónica (las demás son remisiones)
                if (electronicInvoiceSet.Contains(sale.Invoice.InvoiceID))
                {
                    item.SalesVAT += sale.Invoice.Tax;
                }

                // Aproximación de costo usando el costo actual del producto
                item.Cost += sale.Details.Sum(d => d.Quantity * d.Product.Cost);
            }

            // 2. Gastos Fijos
            var fixedExpenses = await _context.FixedExpensePayments
                .Where(p => p.PaymentDate.Year == reportYear)
                .AsNoTracking()
                .ToListAsync();

            foreach (var fe in fixedExpenses)
            {
                var item = model.Items.First(m => m.Month == fe.PaymentDate.Month);
                item.FixedExpenses += fe.Amount;
            }

            // 3. Gastos Local (Movimientos de Caja tipo Egreso, excluyendo los que son pagos de Gastos Fijos)
            var cashExpenses = await _context.CashMovements
                .Where(cm => cm.MovementDate.Year == reportYear && cm.MovementType == CashMovementType.Expense)
                .AsNoTracking()
                .ToListAsync();
            
            // IDs de movimientos de caja que corresponden a gastos fijos para no duplicarlos
            var feCashIds = fixedExpenses.Where(fe => fe.CashMovementId.HasValue).Select(fe => fe.CashMovementId!.Value).ToHashSet();

            foreach (var ce in cashExpenses)
            {
                if (!feCashIds.Contains(ce.CashMovementID))
                {
                    var item = model.Items.First(m => m.Month == ce.MovementDate.Month);
                    item.LocalExpenses += ce.Amount;
                }
            }

            // 4. Compras e IVA Compras
            var purchases = await _context.Purchases
                .Include(p => p.PurchaseDetails)
                .Where(p => p.PurchaseDate.Year == reportYear && p.Status != PurchaseStatus.Cancelled && p.Status != PurchaseStatus.Draft)
                .AsNoTracking()
                .ToListAsync();

            foreach (var p in purchases)
            {
                var item = model.Items.First(m => m.Month == p.PurchaseDate.Month);
                foreach (var d in p.PurchaseDetails)
                {
                    decimal lineTotal = d.Quantity * d.UnitPrice;
                    item.Purchases += lineTotal;
                    item.PurchaseVAT += lineTotal * (d.TaxRate / 100);
                }
            }

            // 5. Cuentas por Cobrar (Generadas en el mes)
            var receivables = await _context.AccountReceivables
                .Where(ar => ar.Date.Year == reportYear)
                .AsNoTracking()
                .ToListAsync();

            foreach (var ar in receivables)
            {
                var item = model.Items.First(m => m.Month == ar.Date.Month);
                item.AccountsReceivable += ar.TotalAmount;
            }

            // 6. Activos (Adquiridos en el mes y con estado Activo)
            var assets = await _context.Assets
                .Where(a => a.AcquisitionDate.Year == reportYear && a.Status == AssetStatus.Active)
                .AsNoTracking()
                .ToListAsync();

            foreach (var asset in assets)
            {
                var item = model.Items.First(m => m.Month == asset.AcquisitionDate.Month);
                item.AssetsValue += asset.AcquisitionCost;
            }

            // Calcular Utilidad y Totales Generales
            foreach (var item in model.Items)
            {
                item.Profit = (item.Sales - item.Cost) - item.FixedExpenses - item.LocalExpenses;
                
                model.Totals.Sales += item.Sales;
                model.Totals.Cost += item.Cost;
                model.Totals.Profit += item.Profit;
                model.Totals.FixedExpenses += item.FixedExpenses;
                model.Totals.LocalExpenses += item.LocalExpenses;
                model.Totals.Purchases += item.Purchases;
                model.Totals.AccountsReceivable += item.AccountsReceivable;
                model.Totals.PurchaseVAT += item.PurchaseVAT;
                model.Totals.SalesVAT += item.SalesVAT;
                model.Totals.AssetsValue += item.AssetsValue;
            }

            return View(model);
        }
    }
}