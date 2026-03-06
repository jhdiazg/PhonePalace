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
using PhonePalace.Domain.Interfaces;
using System.IO;

namespace PhonePalace.Web.Controllers
{
    [Authorize]
    [Route("Reportes")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ICashService _cashService;

        public ReportsController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, ICashService cashService)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _cashService = cashService;
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
                        where p.PaymentDate >= start && p.PaymentDate <= endFilter && p.Invoice!.Status != InvoiceStatus.Cancelled
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
                // CORRECCIÓN: Uso de operador '?' para evitar advertencia de posible nulo (CS8602)
                ClientName = x.Client.DisplayName ?? "Cliente General",
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
            
            // Calcular devoluciones en el periodo para mostrar como dato informativo
            var totalReturns = await _context.Returns
                .Where(r => r.Date >= start && r.Date <= endFilter)
                .SumAsync(r => r.TotalAmount);
            ViewData["TotalReturns"] = totalReturns;

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
        public async Task<IActionResult> BankTransactions(int? bankId, DateTime? startDate, DateTime? endDate, int? pageNumber, int? pageSize)
        {
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today;
            // Se estandariza el tamaño de página a 15 para consistencia con otros reportes.
            var currentPageSize = pageSize ?? 15;
            ViewData["PageSize"] = currentPageSize;

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
                var query = _context.BankTransactions
                    .Where(t => t.BankID == bankId.Value && t.Date >= start && t.Date <= endFilter)
                    // Se añade un ordenamiento secundario para garantizar una paginación estable.
                    .OrderByDescending(t => t.Date).ThenByDescending(t => t.BankTransactionID);

                // Calcular saldo anterior buscando la última transacción antes del rango
                var lastTransactionBefore = await _context.BankTransactions
                    .Where(t => t.BankID == bankId.Value && t.Date < start)
                    .OrderByDescending(t => t.Date)
                    .FirstOrDefaultAsync();

                model.PreviousBalance = lastTransactionBefore?.BalanceAfterTransaction ?? 0;

                // Paginación
                var paginatedList = await PaginatedList<BankTransaction>.CreateAsync(query.AsNoTracking(), pageNumber ?? 1, currentPageSize);
                model.Transactions = paginatedList;
                
                // Pasar objeto paginado a ViewData para usar sus propiedades en la vista (HasPreviousPage, etc.)
                // ya que el ViewModel probablemente espera una List<> genérica.
                ViewData["PaginatedTransactions"] = paginatedList;
            }
            else
            {
                model.Transactions = new List<BankTransaction>();
            }

            return View(model);
        }

        [HttpGet]
        [Route("MovimientosCaja")]
        public async Task<IActionResult> CashMovements(DateTime? reportDate, int? pageNumber, int? pageSize)
        {
            var date = reportDate ?? DateTime.Today;
            ViewData["PageSize"] = pageSize ?? 20;

            var model = new CashMovementReportViewModel
            {
                ReportDate = date,
                IsCashRegisterFound = false
            };

            // Buscar la caja que se abrió en la fecha especificada.
            // Asumimos que solo hay una caja por día. Si pueden haber más, se necesitaría un selector.
            var cashRegister = await _context.CashRegisters
                .AsNoTracking()
                .FirstOrDefaultAsync(cr => cr.OpeningDate.Date == date.Date);

            if (cashRegister != null)
            {
                model.IsCashRegisterFound = true;
                model.OpeningBalance = cashRegister.OpeningAmount;

                // Consulta base para movimientos
                var query = _context.CashMovements
                    .Where(cm => cm.CashRegisterID == cashRegister.CashRegisterID)
                    .OrderBy(m => m.MovementDate);

                // Calcular totales sobre TODOS los movimientos del día (para que el resumen sea correcto)
                var allMovementsForTotals = await _context.CashMovements
                    .Where(cm => cm.CashRegisterID == cashRegister.CashRegisterID)
                    .Select(m => new { m.MovementType, m.Amount })
                    .ToListAsync();

                model.TotalIncome = allMovementsForTotals
                    .Where(m => m.MovementType == CashMovementType.Income || m.MovementType == CashMovementType.Opening)
                    .Sum(m => m.Amount);
                
                model.TotalExpenses = allMovementsForTotals.Where(m => m.MovementType == CashMovementType.Expense).Sum(m => m.Amount);
                
                var incomeOnly = allMovementsForTotals.Where(m => m.MovementType == CashMovementType.Income).Sum(m => m.Amount);
                model.ExpectedBalance = (cashRegister.OpeningAmount + incomeOnly) - model.TotalExpenses;

                // Paginación de la lista a mostrar
                var paginatedMovements = await PaginatedList<CashMovement>.CreateAsync(query.AsNoTracking(), pageNumber ?? 1, pageSize ?? 20);
                model.Movements = paginatedMovements;
                ViewData["PaginatedMovements"] = paginatedMovements;

                // Obtener nombres de usuarios para mostrar en la vista
                var userIds = paginatedMovements.Where(m => m.UserId != null).Select(m => m.UserId).Distinct().ToList();
                ViewBag.UserNames = await _context.Users
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.UserName);
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
                
                // Solo sumar IVA si tiene factura electrónica (las demás son remisiones)
                if (electronicInvoiceSet.Contains(sale.Invoice.InvoiceID))
                {
                    // Para facturas electrónicas, se reporta la venta neta (Subtotal) y el IVA por separado.
                    item.Sales += sale.Invoice.Subtotal;
                    item.SalesVAT += sale.Invoice.Tax;
                }
                else
                {
                    // Para remisiones (ventas locales), se reporta el valor total de la venta.
                    item.Sales += sale.Invoice.Total;
                }

                // Usar costo histórico si existe (ventas nuevas), sino usar costo actual (ventas antiguas)
                item.Cost += sale.Details.Sum(d => d.Quantity * (d.Cost > 0 ? d.Cost : (d.Product?.Cost ?? 0)));
            }

            // 1.1. Restar Devoluciones de Ventas y Costos
            var returns = await _context.Returns
                .Include(r => r.Details)
                .ThenInclude(d => d.Product)
                .Where(r => r.Date.Year == reportYear)
                .AsNoTracking()
                .ToListAsync();

            foreach (var ret in returns)
            {
                var item = model.Items.First(m => m.Month == ret.Date.Month);

                // Restar el valor de la devolución de las ventas.
                item.Sales -= ret.TotalAmount;

                // Restar el costo de los productos devueltos.
                // Usar costo histórico si existe, sino usar costo actual del producto (fallback)
                item.Cost -= ret.Details.Sum(d => d.Quantity * (d.Cost > 0 ? d.Cost : (d.Product?.Cost ?? 0)));
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

            // Prefijos de descripciones que NO son gastos operativos y deben ser excluidos del reporte de utilidad
            var nonOperationalExpensePrefixes = new[]
            {
                "PAGO DE CXP", // Pagos a proveedores (inventario)
                "COMPRA DE ACTIVO", // Compra de activos fijos
                "PRÉSTAMO A CLIENTE", // Salida de dinero que genera una cuenta por cobrar
                "CONSIGNACIÓN A BANCO", // Transferencia de dinero
                "ANULACIÓN VENTA" // Devolución de dinero por venta anulada
            };

            // Palabras clave para gastos de eventos (Rifas, Sorteos) que deben restar de Otros Ingresos en lugar de sumar a Gastos Local
            var eventExpenseKeywords = new[] { "RIFA", "PREMIO", "SORTEO", "EVENTO", "CONCURSO", "CELEBRACION","TAPAZO"};

            foreach (var ce in cashExpenses)
            {
                // Excluir si ya está contado como Gasto Fijo
                if (feCashIds.Contains(ce.CashMovementID))
                {
                    continue;
                }

                var upperDescription = (ce.Description ?? "").ToUpper();

                // Si es un gasto de evento (ej. Premio Rifa), restarlo de Otros Ingresos para mostrar la utilidad neta del evento
                if (eventExpenseKeywords.Any(k => upperDescription.Contains(k)))
                {
                    var eventItem = model.Items.First(m => m.Month == ce.MovementDate.Month);
                    eventItem.OtherIncome -= ce.Amount;
                    continue;
                }

                // Excluir si es un movimiento de capital/financiero y no un gasto operativo
                if (nonOperationalExpensePrefixes.Any(prefix => upperDescription.StartsWith(prefix)))
                {
                    continue;
                }

                // Si pasa los filtros, es un gasto local válido
                var item = model.Items.First(m => m.Month == ce.MovementDate.Month);
                item.LocalExpenses += ce.Amount;
            }

            // 3.1. Gastos Bancarios (Operativos)
            // Sumar a "LocalExpenses" los egresos bancarios manuales que no sean de otras categorías
            var bankExpenses = await _context.BankTransactions
                .Where(bt => bt.Date.Year == reportYear && bt.Amount < 0) // Egresos son negativos
                .AsNoTracking()
                .ToListAsync();

            foreach (var be in bankExpenses)
            {
                var desc = (be.Description ?? "").ToUpper();
                // Excluir movimientos que ya se cuentan en otras secciones o no son gastos operativos puros
                if (desc.Contains("GASTO FIJO") || desc.Contains("ANULACIÓN") || desc.Contains("PRÉSTAMO") || 
                    desc.Contains("COMPRA DE ACTIVO") || desc.Contains("PAGO DE CXP") || desc.Contains("RETIRO HACIA CAJA"))
                {
                    continue;
                }

                // Si es un gasto de evento bancario (ej. Transferencia del premio)
                if (eventExpenseKeywords.Any(k => desc.Contains(k)))
                {
                    var eventItem = model.Items.First(m => m.Month == be.Date.Month);
                    eventItem.OtherIncome -= Math.Abs(be.Amount);
                    continue;
                }
                var item = model.Items.First(m => m.Month == be.Date.Month);
                item.LocalExpenses += Math.Abs(be.Amount);
            }

            // 3.2. Otros Ingresos (que no son ventas de productos)
            // Sumar a "OtherIncome" los ingresos de caja que no son abonos a CxC ni pagos de ventas
            var cashIncomes = await _context.CashMovements
                .Where(cm => cm.MovementDate.Year == reportYear && cm.MovementType == CashMovementType.Income)
                .AsNoTracking()
                .ToListAsync();

            foreach (var ci in cashIncomes)
            {
                var upperDescription = (ci.Description ?? "").ToUpper();
                // Excluir abonos a CxC, pagos por venta y aperturas para evitar duplicidad con Sales o CxC
                if (upperDescription.Contains("ABONO A CXC") || 
                    upperDescription.Contains("POR VENTA") || 
                    upperDescription.Contains("APERTURA") ||
                    upperDescription.Contains("GASTO") || // Excluir pagos de gastos mal clasificados como ingreso
                    upperDescription.Contains("PAGO") ||
                    upperDescription.Contains("SALDO PENDIENTE"))
                {
                    continue;
                }
                var item = model.Items.First(m => m.Month == ci.MovementDate.Month);
                item.OtherIncome += ci.Amount;
            }

            // Sumar a "OtherIncome" los ingresos bancarios manuales
            var bankIncomes = await _context.BankTransactions
                .Where(bt => bt.Date.Year == reportYear && bt.Amount > 0) // Ingresos son positivos
                .AsNoTracking()
                .ToListAsync();

            foreach (var bi in bankIncomes)
            {
                var desc = (bi.Description ?? "").ToUpper();
                // Excluir ingresos que ya se cuentan en ventas, abonos, o son transferencias internas.
                if (desc.Contains("INGRESO POR VENTA") || desc.Contains("ABONO CXC") ||
                    desc.Contains("TRANSFERENCIA") || desc.Contains("RETIRO") || 
                    desc.Contains("DEVOLUCIÓN COMPRA") || desc.Contains("CONSIGNACIÓN") ||
                    desc.Contains("GASTO") || // Excluir devoluciones de gastos o errores
                    desc.Contains("PAGO"))
                {
                    continue;
                }
                var item = model.Items.First(m => m.Month == bi.Date.Month);
                item.OtherIncome += bi.Amount;
            }

            // Sumar a "OtherIncome" las Cuentas por Cobrar de tipo "Otro" (Ingresos devengados no cobrados)
            var otherReceivables = await _context.AccountReceivables
                .Where(ar => ar.Date.Year == reportYear && ar.Type == "Otro")
                .AsNoTracking()
                .ToListAsync();
            
            foreach (var ar in otherReceivables)
            {
                var item = model.Items.First(m => m.Month == ar.Date.Month);
                item.OtherIncome += ar.TotalAmount;
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
                    item.PurchaseVAT += lineTotal * (d.TaxRate / 100);
                }
            }

            // 4.1. Sumar los abonos reales a proveedores (Flujo de Caja)
            // Ahora sumamos los pagos registrados en AccountPayablePayment en lugar de la causación de la compra.
            var supplierPayments = await _context.Set<AccountPayablePayment>()
                .Where(p => p.PaymentDate.Year == reportYear)
                .AsNoTracking()
                .ToListAsync();

            foreach (var sp in supplierPayments)
            {
                var item = model.Items.First(m => m.Month == sp.PaymentDate.Month);
                item.Purchases += sp.Amount;
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
                item.Profit = (item.Sales + item.OtherIncome - item.Cost) - item.FixedExpenses - item.LocalExpenses;
                
                model.Totals.Sales += item.Sales;
                model.Totals.Cost += item.Cost;
                model.Totals.OtherIncome += item.OtherIncome;
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

        [HttpGet]
        [Route("DetalleOtrosIngresos")]
        public async Task<IActionResult> GetOtherIncomeDetails(int month, int year)
        {
            var rawDetails = new List<(DateTime Date, string Source, string? Description, decimal Amount)>();

            // 1. Caja: Ingresos que no son abonos, ventas ni aperturas
            var cashIncomes = await _context.CashMovements
                .Where(cm => cm.MovementDate.Year == year && cm.MovementDate.Month == month && cm.MovementType == CashMovementType.Income)
                .AsNoTracking()
                .ToListAsync();

            foreach (var ci in cashIncomes)
            {
                var upperDescription = (ci.Description ?? "").ToUpper();
                if (upperDescription.Contains("ABONO A CXC") || 
                    upperDescription.Contains("POR VENTA") || 
                    upperDescription.Contains("APERTURA") ||
                    upperDescription.Contains("GASTO") ||
                    upperDescription.Contains("PAGO") ||
                    upperDescription.Contains("SALDO PENDIENTE"))
                {
                    continue;
                }
                rawDetails.Add((ci.MovementDate, "Caja", ci.Description, ci.Amount));
            }

            // 2. Bancos: Ingresos manuales que no son ventas, transferencias, etc.
            var bankIncomes = await _context.BankTransactions
                .Where(bt => bt.Date.Year == year && bt.Date.Month == month && bt.Amount > 0)
                .AsNoTracking()
                .ToListAsync();

            foreach (var bi in bankIncomes)
            {
                var desc = (bi.Description ?? "").ToUpper();
                if (desc.Contains("INGRESO POR VENTA") || desc.Contains("ABONO CXC") ||
                    desc.Contains("TRANSFERENCIA") || desc.Contains("RETIRO") || 
                    desc.Contains("DEVOLUCIÓN COMPRA") || desc.Contains("CONSIGNACIÓN") ||
                    desc.Contains("GASTO") ||
                    desc.Contains("PAGO"))
                {
                    continue;
                }
                rawDetails.Add((bi.Date, "Banco", bi.Description, bi.Amount));
            }

            // 3. CxC: Cuentas por cobrar de tipo "Otro" (Ingresos devengados)
            var otherReceivables = await _context.AccountReceivables
                .Where(ar => ar.Date.Year == year && ar.Date.Month == month && ar.Type == "Otro")
                .AsNoTracking()
                .ToListAsync();
            
            foreach (var ar in otherReceivables)
            {
                rawDetails.Add((ar.Date, "CxC (Crédito)", ar.Description, ar.TotalAmount));
            }

            // 4. Restar Egresos de Eventos (Rifas, etc.) para mostrar el neto en el detalle
            var eventExpenseKeywords = new[] { "RIFA", "PREMIO", "SORTEO", "EVENTO", "CONCURSO", "CELEBRACION", "TAPAZO" };
            
            var cashExpenses = await _context.CashMovements
                .Where(cm => cm.MovementDate.Year == year && cm.MovementDate.Month == month && cm.MovementType == CashMovementType.Expense)
                .AsNoTracking()
                .ToListAsync();

            foreach (var ce in cashExpenses)
            {
                if (eventExpenseKeywords.Any(k => (ce.Description ?? "").ToUpper().Contains(k)))
                {
                    rawDetails.Add((ce.MovementDate, "Gasto Evento (Caja)", ce.Description ?? "???", -ce.Amount));
                }
            }

            var bankExpenses = await _context.BankTransactions
                .Where(bt => bt.Date.Year == year && bt.Date.Month == month && bt.Amount < 0)
                .AsNoTracking()
                .ToListAsync();

            foreach (var be in bankExpenses)
            {
                if (eventExpenseKeywords.Any(k => (be.Description ?? "").ToUpper().Contains(k)))
                {
                    rawDetails.Add((be.Date, "Gasto Evento (Banco)", be.Description, be.Amount)); // Amount ya es negativo
                }
            }

            var result = rawDetails.OrderBy(x => x.Date).Select(x => new 
            {
                Date = x.Date.ToString("dd/MM/yyyy"),
                Source = x.Source,
                Description = x.Description,
                Amount = x.Amount
            });

            return Json(result);
        }

        [HttpGet]
        [Route("BalanceGeneral")]
        public async Task<IActionResult> GeneralBalance()
        {
            var model = new GeneralBalanceViewModel
            {
                ReportDate = DateTime.Now
            };

            // 1. ACTIVOS
            // Inventario: Suma del costo de todos los productos en stock
            model.InventoryValue = await _context.Inventories
                .SumAsync(i => i.Stock * i.Product.Cost);

            // Cuentas por Cobrar: Saldo pendiente de clientes
            model.AccountsReceivable = await _context.AccountReceivables
                .Where(ar => !ar.IsPaid)
                .SumAsync(ar => ar.Balance);

            // Efectivo: Saldo actual de caja
            // Si hay caja abierta, usamos el saldo actual. Si no, usamos el último cierre (dinero en custodia).
            var currentCashRegister = await _cashService.GetCurrentCashRegisterAsync();
            if (currentCashRegister != null)
            {
                model.Cash = await _cashService.GetCurrentBalanceAsync();
            }
            else
            {
                var lastClosed = await _context.CashRegisters
                    .Where(cr => cr.ClosingDate != null)
                    .OrderByDescending(cr => cr.ClosingDate)
                    .Select(cr => cr.ClosingAmount)
                    .FirstOrDefaultAsync();
                model.Cash = lastClosed ?? 0;
            }

            // Bancos: Separar Nequi y Daviplata del resto
            // CORRECCIÓN: Incluir bancos inactivos para que el total coincida con el activo real.
            var allBanks = await _context.Banks.ToListAsync();
            
            model.Nequi = allBanks.Where(b => b.Name.ToUpper().Contains("NEQUI")).Sum(b => b.Balance);
            model.Daviplata = allBanks.Where(b => b.Name.ToUpper().Contains("DAVIPLATA")).Sum(b => b.Balance);
            // El resto de bancos que no son ni Nequi ni Daviplata
            model.Banks = allBanks.Where(b => !b.Name.ToUpper().Contains("NEQUI") && !b.Name.ToUpper().Contains("DAVIPLATA")).Sum(b => b.Balance);

            // Activos Fijos: Valor de adquisición de activos activos
            model.FixedAssets = await _context.Assets
                .Where(a => a.Status == AssetStatus.Active)
                .SumAsync(a => a.AcquisitionCost);

            // 2. PASIVOS
            // Definir tipos que se consideran "Créditos / Obligaciones Financieras"
            var creditTypes = new[] { "CreditoBancario", "Impuestos", "Otros", "Prestamo" };

            // Cuentas por Pagar: Todo lo que NO sea explícitamente un crédito (Proveedores, Compras, null, vacíos)
            model.AccountsPayable = await _context.AccountPayables
                .Where(ap => !ap.IsPaid && !creditTypes.Contains(ap.Type))
                .SumAsync(ap => ap.Balance);

            // Créditos: Solo los tipos específicos
            model.Credits = await _context.AccountPayables
                .Where(ap => !ap.IsPaid && creditTypes.Contains(ap.Type))
                .SumAsync(ap => ap.Balance);

            return View(model);
        }
    }
}