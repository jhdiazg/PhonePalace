using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Configuration;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.Helpers;
using PhonePalace.Web.Documents;
using System.Security.Claims;
using QuestPDF.Fluent;
using ClosedXML.Excel;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using PhonePalace.Infrastructure.Services;

namespace PhonePalace.Web.Controllers
{
    [Route("Ventas")]
    [Authorize(Roles = "Administrador,Vendedor")]
    public class SalesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICashService _cashService;
        private readonly IBankService _bankService;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly CompanySettings _companySettings;
        private readonly IPlemsiService _plemsiService;
        private readonly IAuditService _auditService;
        private readonly IEmailSender _emailSender;

        public SalesController(ApplicationDbContext context, ICashService cashService, IBankService bankService, IConfiguration config, IWebHostEnvironment webHostEnvironment, IOptions<CompanySettings> companySettings, IPlemsiService plemsiService, IAuditService auditService, IEmailSender emailSender)
        {
            _context = context;
            _cashService = cashService;
            _bankService = bankService;
            _config = config;
            _webHostEnvironment = webHostEnvironment;
            _companySettings = companySettings.Value;
            _plemsiService = plemsiService;
            _auditService = auditService;
            _emailSender = emailSender;
        }

    [HttpGet]
    [Route("")]
    public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, string? clientName, int? pageNumber, int? pageSize)
        {
            // Validaciones de fechas
            bool datesAdjusted = false;
            DateTime? originalStartDate = startDate;
            DateTime? originalEndDate = endDate;

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
                // Redirigir con fechas corregidas
                return RedirectToAction("Index", new
                {
                    startDate = startDate?.ToString("yyyy-MM-dd"),
                    endDate = endDate?.ToString("yyyy-MM-dd"),
                    clientName
                });
            }

            ViewData["PageSize"] = pageSize ?? 10;

            // Obtener caja actual para validaciones de anulación en la vista
            var currentCash = await _cashService.GetCurrentCashRegisterAsync();
            ViewData["CurrentCashDate"] = currentCash?.OpeningDate.Date;

            // Por defecto, mostrar solo ventas del día actual
            if (!startDate.HasValue && !endDate.HasValue && string.IsNullOrEmpty(clientName))
            {
                // Si hay una caja abierta, mostrar ventas de esa fecha de apertura por defecto
                if (currentCash != null)
                {
                    startDate = currentCash.OpeningDate.Date;
                    endDate = currentCash.OpeningDate.Date;
                }
                else
                {
                    startDate = DateTime.Now.Date;
                    endDate = DateTime.Now.Date;
                }
            }

            var salesQuery = _context.Sales
                .Include(s => s.Client)
                .Include(s => s.Invoice)
                .Include(s => s.Details)
                .ThenInclude(d => d.Product)
                .AsQueryable();

            if (startDate.HasValue)
            {
                salesQuery = salesQuery.Where(s => s.SaleDate.Date >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                salesQuery = salesQuery.Where(s => s.SaleDate.Date <= endDate.Value.Date);
            }

            if (!string.IsNullOrEmpty(clientName))
            {
                salesQuery = salesQuery.Where(s => 
                    (s.Client is NaturalPerson && (
                        ((NaturalPerson)s.Client).FirstName.Contains(clientName) || 
                        ((NaturalPerson)s.Client).LastName.Contains(clientName) ||
                        (((NaturalPerson)s.Client).FirstName + " " + ((NaturalPerson)s.Client).LastName).Contains(clientName))) ||
                    (s.Client is LegalEntity && ((LegalEntity)s.Client).CompanyName.Contains(clientName)));
            }

            // Calcular total de la consulta antes de paginar
            decimal total = await salesQuery.SumAsync(s => s.Invoice.Total);
            // Calcular utilidad total (Venta - Costo)
            decimal totalProfit = await salesQuery.SelectMany(s => s.Details).SumAsync(d => d.Quantity * (d.UnitPrice - d.Product.Cost));

            var sales = await PaginatedList<Sale>.CreateAsync(salesQuery.OrderByDescending(s => s.SaleDate).AsNoTracking(), pageNumber ?? 1, pageSize ?? 10);

            // Identificar ventas con saldo pendiente (Crédito) para mostrar icono en el Index
            var saleIds = sales.Select(s => s.SaleID).ToList();
            var pendingSaleIds = await _context.AccountReceivables
                .Where(ar => ar.SaleID.HasValue && saleIds.Contains(ar.SaleID.Value) && !ar.IsPaid)
                .Select(ar => ar.SaleID!.Value)
                .ToListAsync();
            ViewData["PendingSaleIds"] = new HashSet<int>(pendingSaleIds);

            ViewData["StartDate"] = startDate?.ToString("yyyy-MM-dd");
            ViewData["EndDate"] = endDate?.ToString("yyyy-MM-dd");
            ViewData["ClientName"] = clientName;
            ViewData["Total"] = total;
            ViewData["TotalProfit"] = totalProfit;

            return View(sales);
        }

    [HttpGet]
    [Route("Details/{id?}")]
    public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sale = await _context.Sales
                .Include(s => s.Client)
                .Include(s => s.Invoice)
                .ThenInclude(i => i.Payments)
                .Include(s => s.Details)
                .ThenInclude(sd => sd.Product)
                .FirstOrDefaultAsync(m => m.SaleID == id);

            if (sale == null)
            {
                return NotFound();
            }

            // Obtener información de la cuenta por cobrar (crédito) asociada si existe
            var creditInfo = await _context.AccountReceivables
                .AsNoTracking()
                .FirstOrDefaultAsync(ar => ar.SaleID == id);

            // FALLBACK: Si no se encuentra por ID (ventas antiguas), buscar por descripción de factura
            if (creditInfo == null && sale.Invoice != null)
            {
                string invoiceIdStr = sale.Invoice.InvoiceID.ToString();
                creditInfo = await _context.AccountReceivables
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ar => ar.ClientID == sale.ClientID && ar.Description != null && ar.Description.Contains(invoiceIdStr));
            }

            // FALLBACK 2: Buscar por coincidencia de fecha y cliente (para ventas antiguas sin referencia clara)
            if (creditInfo == null)
            {
                creditInfo = await _context.AccountReceivables
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ar => ar.ClientID == sale.ClientID && ar.Date.Date == sale.SaleDate.Date && ar.Type == "Venta" && ar.SaleID == null);
            }

            ViewBag.CreditInfo = creditInfo;

            // Cargar información de Factura Electrónica si existe (Entidad Independiente)
            if (sale.Invoice != null)
            {
                var electronicInvoice = await _context.Set<ElectronicInvoice>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.InvoiceID == sale.Invoice.InvoiceID);

                if (electronicInvoice != null && !string.IsNullOrEmpty(electronicInvoice.CUFE))
                {
                    // Recalcular URL de la DIAN para asegurar que el botón funcione correctamente
                    string plemsiUrl = _config.GetValue<string>("Plemsi:BaseUrl") ?? "";
                    bool isHab = plemsiUrl.Contains("pruebas", StringComparison.OrdinalIgnoreCase) || plemsiUrl.Contains("sandbox", StringComparison.OrdinalIgnoreCase);
                    electronicInvoice.QRCodeUrl = isHab ? $"https://catalogo-vpfe-hab.dian.gov.co/document/searchqr?documentkey={electronicInvoice.CUFE}" : $"https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey={electronicInvoice.CUFE}";
                }
                ViewBag.ElectronicInvoice = electronicInvoice;
            }

            // Obtener caja actual para validaciones de anulación en la vista
            var currentCash = await _cashService.GetCurrentCashRegisterAsync();
            ViewData["CurrentCashDate"] = currentCash?.OpeningDate.Date;

            return View(sale);
        }
    [HttpGet]
    [Route("Create")]
    public async Task<IActionResult> Create()
        {
            // Validar que la caja esté abierta antes de permitir registrar venta
            var currentCash = await _cashService.GetCurrentCashRegisterAsync();
            if (currentCash == null)
            {
                TempData["Error"] = "La caja está cerrada. Debe abrirla para registrar ventas.";
                return RedirectToAction(nameof(Index));
            }

            var viewModel = new ViewModels.SaleCreateViewModel();
            
            // Pre-llenar fecha con la fecha de apertura de caja
            viewModel.SaleDate = currentCash.OpeningDate;

            if (currentCash.OpeningDate.Date != DateTime.Now.Date)
            {
                TempData["Warning"] = $"ATENCIÓN: La caja tiene fecha de apertura del {currentCash.OpeningDate:dd/MM/yyyy}, que es diferente a la fecha actual. La venta quedará registrada con la fecha de la caja.";
            }

            // Inicializa dropdowns
            viewModel.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>();
            viewModel.SaleChannels = new SelectList(EnumHelper.ToSelectList<SaleChannel>().Where(x => x.Value != "0"), "Value", "Text");
            viewModel.Clients = new SelectList(_context.Clients.Where(c => c.IsActive), "ClientID", "DisplayName");
            viewModel.Products = new SelectList(_context.Products.Where(p => p.IsActive), "ProductID", "Name");
            ViewBag.PriceListTypes = new SelectList(Enum.GetValues(typeof(PriceListType)).Cast<PriceListType>().Select(e => new { Value = (int)e, Text = EnumHelper.GetDisplayName(e) }), "Value", "Text");
            ViewBag.Categories = new SelectList(await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(), "CategoryID", "Name");
            ViewBag.AllProducts = _context.Products.Where(p => p.IsActive).ToList();
            ViewBag.Banks = new SelectList(await _context.Banks.Where(b => b.IsActive).ToListAsync(), "BankID", "Name");
            ViewBag.DefaultCardBankId = _config.GetValue<int?>("Defaults:CardBankID");
            
            var taxRate = _config.GetValue<decimal>("TaxSettings:IVARate");
            if (taxRate > 1) taxRate /= 100; // Normalizar si viene como entero (ej. 19 -> 0.19)
            ViewBag.TaxRate = taxRate;
            ViewBag.CardSurchargePercentage = _config.GetValue<decimal>("SalesSettings:CardSurchargePercentage");

            // Si hay datos en TempData (por conversión de cotización), los carga y re-inicializa dropdowns
            if (TempData["SaleModel"] is string saleJson)
            {
                var tempModel = System.Text.Json.JsonSerializer.Deserialize<ViewModels.SaleCreateViewModel>(saleJson);
                if (tempModel != null)
                {
                    viewModel = tempModel;
                    viewModel.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>();
                    viewModel.SaleChannels = new SelectList(EnumHelper.ToSelectList<SaleChannel>().Where(x => x.Value != "0"), "Value", "Text");
                    viewModel.Clients = new SelectList(_context.Clients.Where(c => c.IsActive), "ClientID", "DisplayName");
                    viewModel.Products = new SelectList(_context.Products.Where(p => p.IsActive), "ProductID", "Name");
                    ViewBag.PriceListTypes = new SelectList(EnumHelper.ToSelectList<PriceListType>(), "Value", "Text");
                    ViewBag.Categories = new SelectList(await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(), "CategoryID", "Name");
                    ViewBag.AllProducts = _context.Products.Where(p => p.IsActive).ToList();
                    ViewBag.Banks = new SelectList(await _context.Banks.Where(b => b.IsActive).ToListAsync(), "BankID", "Name");
                    ViewBag.TaxRate = taxRate;
                }
            }
            return View(viewModel);
        }

    [HttpPost]
    [Route("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ViewModels.SaleCreateViewModel viewModel)
        {
            viewModel.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>();
            viewModel.SaleChannels = new SelectList(EnumHelper.ToSelectList<SaleChannel>().Where(x => x.Value != "0"), "Value", "Text");
            viewModel.Clients = new SelectList(_context.Clients.Where(c => c.IsActive), "ClientID", "DisplayName");
            viewModel.Products = new SelectList(_context.Products.Where(p => p.IsActive), "ProductID", "Name");
            ViewBag.PriceListTypes = new SelectList(Enum.GetValues(typeof(PriceListType)).Cast<PriceListType>().Select(e => new { Value = (int)e, Text = EnumHelper.GetDisplayName(e) }), "Value", "Text");
            ViewBag.Categories = new SelectList(await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(), "CategoryID", "Name");
            ViewBag.AllProducts = _context.Products.Where(p => p.IsActive).ToList();
            ViewBag.Banks = new SelectList(await _context.Banks.Where(b => b.IsActive).ToListAsync(), "BankID", "Name");
            ViewBag.DefaultCardBankId = _config.GetValue<int?>("Defaults:CardBankID");
            
            var taxRate = _config.GetValue<decimal>("TaxSettings:IVARate");
            if (taxRate > 1) taxRate /= 100; // Normalizar
            ViewBag.TaxRate = taxRate;
            ViewBag.CardSurchargePercentage = _config.GetValue<decimal>("SalesSettings:CardSurchargePercentage");

            // Validaciones de caja
            var currentCash = await _cashService.GetCurrentCashRegisterAsync();
            if (currentCash == null)
            {
                ModelState.AddModelError("", "Debe abrir la caja antes de registrar ventas.");
            }
            else
            {
                // Validación de fecha: debe coincidir con la apertura de caja
                if (viewModel.SaleDate.Date != currentCash.OpeningDate.Date)
                {
                    ModelState.AddModelError("SaleDate", $"La fecha de venta debe ser la fecha de apertura de caja ({currentCash.OpeningDate:dd/MM/yyyy}).");
                }
            }
            // Debug: Log payments
            TempData["Info"] = $"Payments: {string.Join(", ", viewModel.Payments?.Select(p => $"{p.PaymentMethod}:{p.Amount}") ?? new string[0])}";

            if (viewModel.ClientID == null)
            {
                ModelState.AddModelError("ClientID", "Debe seleccionar un cliente.");
            }

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            // Validar que haya productos en la venta
            if (viewModel.Details == null || !viewModel.Details.Any())
            {
                ModelState.AddModelError("", "Debe agregar al menos un producto a la venta.");
                return View(viewModel);
            }

            // Validar Stock Disponible antes de procesar
            foreach (var detailVM in viewModel.Details)
            {
                var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductID == detailVM.ProductID);
                var product = await _context.Products.FindAsync(detailVM.ProductID);
                
                if (inventory == null || inventory.Stock < detailVM.Quantity)
                {
                    ModelState.AddModelError("", $"Stock insuficiente para el producto '{product?.Name ?? "Desconocido"}'. Disponible: {inventory?.Stock ?? 0}, Solicitado: {detailVM.Quantity}");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                return await strategy.ExecuteAsync<IActionResult>(async () =>
                {
                    // Limpiar ChangeTracker para evitar duplicados en caso de reintento
                    _context.ChangeTracker.Clear();

                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        // Obtener el cliente
                        var client = await _context.Clients.FindAsync(viewModel.ClientID!.Value);
                        if (client == null)
                        {
                            ModelState.AddModelError("ClientID", "Cliente no encontrado.");
                            return View(viewModel);
                        }

                        // Calcular totales (El precio unitario YA INCLUYE IVA)
                        decimal total = 0;
                        if (viewModel.Details != null)
                        {
                            foreach (var detailVM in viewModel.Details)
                            {
                                var product = await _context.Products.FindAsync(detailVM.ProductID);
                                if (product == null) continue;

                                // Validar rango de precio (Costo <= Precio <= Costo * 3)
                                if (detailVM.UnitPrice < product.Cost || detailVM.UnitPrice > product.Cost * 3)
                                {
                                    ModelState.AddModelError("", $"El precio del producto '{product.Name}' debe estar entre {product.Cost:C} y {(product.Cost * 3):C}.");
                                    return View(viewModel);
                                }

                                decimal price = detailVM.UnitPrice; // Usar el precio definido por el usuario
                                total += detailVM.Quantity * price;
                            }
                        }

                        // Calcular Recargo por Tarjeta (Si aplica)
                        decimal surchargePercentage = _config.GetValue<decimal>("SalesSettings:CardSurchargePercentage");
                        decimal totalSurcharge = 0;

                        if (viewModel.Payments != null && surchargePercentage > 0)
                        {
                            foreach (var p in viewModel.Payments)
                            {
                                if (Enum.TryParse<PaymentMethod>(p.PaymentMethod, out var method) && 
                                   (method == PaymentMethod.CreditCard || method == PaymentMethod.DebitCard))
                                {
                                    // El monto que llega (p.Amount) ya incluye el recargo. Desglosamos para sumar al total.
                                    decimal rateFactor = 1 + (surchargePercentage / 100m);
                                    decimal principal = p.Amount / rateFactor;
                                    totalSurcharge += (p.Amount - principal);
                                }
                            }
                        }

                        // Validar Saldo a Favor (CustomerBalance)
                        var balancePayments = viewModel.Payments?
                            .Where(p => Enum.TryParse<PaymentMethod>(p.PaymentMethod, true, out var pm) && pm == PaymentMethod.CustomerBalance)
                            .ToList();

                        if (balancePayments != null && balancePayments.Any())
                        {
                            decimal totalBalanceUsed = balancePayments.Sum(p => p.Amount);
                            if (client.Balance < totalBalanceUsed)
                            {
                                throw new Exception($"El cliente no tiene suficiente saldo a favor. Saldo actual: {client.Balance:C}, Intentando usar: {totalBalanceUsed:C}");
                            }
                            // Descontar del saldo del cliente
                            client.Balance -= totalBalanceUsed;
                            _context.Update(client);
                        }
                        total += totalSurcharge;

                        // Desglosar IVA: Total = Subtotal * (1 + Tasa) => Subtotal = Total / (1 + Tasa)
                        decimal subtotal = total / (1 + taxRate);
                        decimal tax = total - subtotal;

                        // Crear la factura (Invoice) asociada
                        var invoice = new Invoice
                        {
                            ClientID = client.ClientID,
                            Client = client,
                            SaleDate = viewModel.SaleDate,
                            SaleChannel = viewModel.SaleChannel ?? SaleChannel.InStore,
                            UserId = User?.Identity?.Name,
                            Subtotal = subtotal,
                            Tax = tax,
                            Total = total,
                            Status = InvoiceStatus.Completed // Inicialmente es una Remisión / Venta Local
                            // IsElectronic = false (Por defecto)
                        };
                        _context.Invoices.Add(invoice);
                        await _context.SaveChangesAsync();

                        // Crear payments si hay
                        var payments = new List<Payment>();
                        if (viewModel.Payments != null && viewModel.Payments.Any())
                        {
                            foreach (var paymentVM in viewModel.Payments)
                            {
                                if (!Enum.TryParse<PaymentMethod>(paymentVM.PaymentMethod, out var paymentMethod))
                                {
                                    continue; // Skip invalid payment methods
                                }
                                var payment = new Payment
                                {
                                    InvoiceID = invoice.InvoiceID,
                                    Invoice = invoice,
                                    PaymentMethod = paymentMethod,
                                    Amount = paymentVM.Amount,
                                    ReferenceNumber = paymentVM.ReferenceNumber,
                                    BankID = paymentVM.BankID
                                };
                                payments.Add(payment);
                                _context.Payments.Add(payment);
                            }
                            await _context.SaveChangesAsync();
                        }

                        // Crear la venta
                        var sale = new Sale
                        {
                            ClientID = client.ClientID,
                            Client = client,
                            InvoiceID = invoice.InvoiceID,
                            Invoice = invoice,
                            SaleDate = viewModel.SaleDate,
                            TotalAmount = total,
                            Details = new List<SaleDetail>()
                        };

                        // Crear los detalles de la venta
                        if (viewModel.Details != null)
                        {
                            foreach (var detailVM in viewModel.Details)
                            {
                                var product = await _context.Products.FindAsync(detailVM.ProductID);
                                if (product == null) continue;
                                var detail = new SaleDetail
                                {
                                    ProductID = detailVM.ProductID,
                                    Product = product,
                                    Quantity = detailVM.Quantity,
                                    UnitPrice = detailVM.UnitPrice, // Usar el precio validado
                                    Sale = sale,
                                    IMEI = detailVM.IMEI?.ToUpper(),
                                    Serial = detailVM.Serial?.ToUpper()
                                };
                                sale.Details.Add(detail);
                            }
                        }

                        _context.Sales.Add(sale);
                        await _context.SaveChangesAsync();

                        // Registrar ingresos en caja, bancos o crear verificaciones de tarjeta de crédito
                        if (payments.Any())
                        {
                            var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
                            if (!string.IsNullOrEmpty(userId))
                            {
                                // The 'payments' list now contains entities with their IDs after SaveChangesAsync
                                foreach (var p in payments)
                                {
                                    if (p.PaymentMethod == PaymentMethod.Cash)
                                    {
                                        await _cashService.RegisterIncomeAsync(p.Amount, $"Pago en efectivo por venta #{invoice.InvoiceID}", userId, p.PaymentID);
                                    }
                                    else if (p.PaymentMethod == PaymentMethod.CreditCard && p.BankID.HasValue)
                                    {
                                        var verification = new CreditCardVerification
                                        {
                                            SaleID = sale.SaleID,
                                            PaymentID = p.PaymentID,
                                            BankID = p.BankID.Value,
                                            Amount = p.Amount,
                                            CreationDate = DateTime.Now,
                                            Status = VerificationStatus.Pending
                                        };
                                        _context.CreditCardVerifications.Add(verification);
                                    }
                                    else if (p.BankID.HasValue) // Otros pagos bancarios
                                    {
                                        await _bankService.RegisterIncomeFromPaymentAsync(p);
                                    }
                                    // CustomerBalance: No hace nada en Caja ni Bancos, ya se descontó del saldo del cliente arriba.
                                }
                            }
                        }

                        // --- INICIO: Generar Cuenta por Cobrar si hay saldo ---
                        // Excluir pagos de tipo "Credit" (Crédito) del cálculo de lo pagado, 
                        // para que se genere la cuenta por cobrar por ese monto.
                        decimal totalPaid = viewModel.Payments?
                            .Where(p => p.PaymentMethod != PaymentMethod.Credit.ToString())
                            .Sum(p => p.Amount) ?? 0;
                        decimal balance = total - totalPaid;

                        if (balance > 0.01m)
                        {
                            var accountReceivable = new AccountReceivable
                            {
                                ClientID = client.ClientID,
                                Client = client,
                                Date = viewModel.SaleDate,
                                TotalAmount = balance,
                                Balance = balance,
                                Type = "Venta",
                                SaleID = sale.SaleID,
                                Sale = sale,
                                Description = $"Saldo pendiente venta #{invoice.InvoiceID}",
                                IsPaid = false
                            };
                            _context.AccountReceivables.Add(accountReceivable);
                            await _context.SaveChangesAsync();
                        }
                        // --- FIN: Generar Cuenta por Cobrar ---

                        // Reducir inventario
                        foreach (var detail in sale.Details)
                        {
                            var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductID == detail.ProductID);
                            if (inventory != null)
                            {
                                inventory.Stock -= detail.Quantity;
                                _context.Update(inventory);
                            }
                            else
                            {
                                // Esto no debería ocurrir gracias a la validación previa, pero es bueno tenerlo controlado
                                throw new Exception($"Error crítico: No se encontró inventario para el producto {detail.ProductID} al finalizar la venta.");
                            }
                        }
                        await _context.SaveChangesAsync();

                        await transaction.CommitAsync();
                        return RedirectToAction(nameof(Index));
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error al procesar la venta: {ex.Message}");
                return View(viewModel);
            }
        }

        [HttpGet]
        [Route("Delete/{id}")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sale = await _context.Sales
                .Include(s => s.Client)
                .Include(s => s.Invoice)
                .FirstOrDefaultAsync(m => m.SaleID == id);
            if (sale == null)
            {
                return NotFound();
            }

            return View(sale);
        }

        [HttpPost, ActionName("Delete")]
        [Route("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<IActionResult>(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var sale = await _context.Sales
                        .Include(s => s.Client)
                        .Include(s => s.Details)
                            .ThenInclude(d => d.Product)
                        .Include(s => s.Invoice)
                            .ThenInclude(i => i.Payments)
                        .FirstOrDefaultAsync(s => s.SaleID == id);

                    if (sale != null)
                    {
                        // 1. Restaurar inventario
                        foreach (var detail in sale.Details)
                        {
                            var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductID == detail.ProductID);
                            if (inventory != null)
                            {
                                inventory.Stock += detail.Quantity;
                                _context.Update(inventory);
                            }
                        }

                        // 2. Reversar Dinero (Caja y Bancos)
                        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                        if (sale.Invoice != null && sale.Invoice.Payments != null)
                        {
                            foreach (var payment in sale.Invoice.Payments)
                            {
                                if (payment.PaymentMethod == PaymentMethod.Cash)
                                {
                                    // Validar si hay caja abierta para registrar el egreso (devolución)
                                    var currentCash = await _cashService.GetCurrentCashRegisterAsync();
                                    if (currentCash != null && !string.IsNullOrEmpty(userId))
                                    {
                                        await _cashService.RegisterExpenseAsync(payment.Amount, $"ANULACIÓN Venta #{sale.Invoice.InvoiceID}", userId);
                                    }
                                }
                                else if (payment.BankID.HasValue)
                                {
                                    // Registrar egreso bancario
                                    await _bankService.RegisterManualMovementAsync(payment.BankID.Value, BankTransactionType.ManualExpense, payment.Amount, $"ANULACIÓN Venta #{sale.Invoice.InvoiceID}");
                                }
                            }
                            
                            // Actualizar estado de la factura
                            sale.Invoice.Status = InvoiceStatus.Cancelled;
                            _context.Update(sale.Invoice);
                        }

                        // 3. Eliminar Cuenta por Cobrar asociada (si existe)
                        var ar = await _context.AccountReceivables.FirstOrDefaultAsync(x => x.SaleID == id);
                        if (ar != null)
                        {
                            _context.AccountReceivables.Remove(ar);
                        }

                        // 4. Marcar venta como eliminada
                        sale.IsDeleted = true;
                        _context.Update(sale);
                        
                        // 5. Emitir Nota Crédito Electrónica si aplica
                        var electronicInvoice = await _context.Set<ElectronicInvoice>()
                            .FirstOrDefaultAsync(e => e.InvoiceID == sale.Invoice.InvoiceID && e.Status == "Accepted");

                        if (electronicInvoice != null)
                        {
                            try 
                            {
                                var ncResponse = await _plemsiService.SendCreditNoteAsync(sale, "Anulación completa de venta por solicitud del cliente", electronicInvoice.CUFE);
                                if (ncResponse.Success)
                                {
                                    await _auditService.LogAsync("Facturación", $"Nota Crédito emitida para factura {sale.Invoice.InvoiceID}. Número: {ncResponse.Number}");
                                }
                                else
                                {
                                    await _auditService.LogAsync("Error Facturación", $"Fallo al emitir Nota Crédito para {sale.Invoice.InvoiceID}: {ncResponse.ErrorMessage}");
                                }
                            }
                            catch (Exception ex) { /* Loguear error pero no detener la anulación local */ }
                        }

                        await _context.SaveChangesAsync();
                        await _auditService.LogAsync("Ventas", $"Anuló la venta #{id} y reversó sus movimientos.");
                        
                        await transaction.CommitAsync();
                    }
                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        [HttpGet]
        [Route("GenerarFactura/{id}")]
        public async Task<IActionResult> GenerateInvoicePdf(int id)
        {
            // 1. Obtener la Venta con todos sus detalles y la Factura asociada
            var sale = await _context.Sales
                .Include(s => s.Invoice)
                    .ThenInclude(i => i.Client) // Cargar el cliente asociado a la factura
                .Include(s => s.Invoice)
                    .ThenInclude(i => i.Payments)
                .Include(s => s.Details)
                    .ThenInclude(sd => sd.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.SaleID == id);

            if (sale == null || sale.Invoice == null)
            {
                return NotFound();
            }

            // 2. Mapeo en memoria: Como Sale.Details tiene los productos, se los pasamos a Invoice.Details
            // para que el generador de PDF tenga qué imprimir.
            if (sale.Invoice.Details == null || !sale.Invoice.Details.Any())
            {
                sale.Invoice.Details = sale.Details.Select(d => new InvoiceDetail
                {
                    ProductID = d.ProductID,
                    Product = d.Product,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice
                }).ToList();
            }

            // 3. Obtener Logo
            string wwwRootPath = _webHostEnvironment.WebRootPath;
            string logoPath = Path.Combine(wwwRootPath, "images", "Logo_fact.png");
            byte[] logoBytes = Array.Empty<byte>();

            if (System.IO.File.Exists(logoPath))
            {
                logoBytes = await System.IO.File.ReadAllBytesAsync(logoPath);
            }

            string sellerName = sale.Invoice.UserId ?? "N/A";
            // 4. Generar PDF usando la configuración centralizada (_companySettings)
            var document = new InvoicePdfDocument(sale.Invoice, logoBytes, _companySettings, sellerName);
            var pdfBytes = document.GeneratePdf();

            return File(pdfBytes, "application/pdf", $"Factura-{sale.Invoice.InvoiceID}.pdf");
        }

        [HttpGet]
        [Route("PlantillaVenta")]
        public async Task<IActionResult> GenerateSaleTemplate()
        {
            using (var workbook = new XLWorkbook())
            {
                // --- Hoja 1: Plantilla para ingresar ventas ---
                var salesSheet = workbook.Worksheets.Add("PlantillaVentas");
                var currentRow = 1;

                // Cabeceras
                salesSheet.Cell(currentRow, 1).Value = "SKU_o_Codigo";
                salesSheet.Cell(currentRow, 2).Value = "Cantidad";
                salesSheet.Cell(currentRow, 3).Value = "PrecioUnitario";
                salesSheet.Cell(currentRow, 4).Value = "IMEI";
                salesSheet.Cell(currentRow, 5).Value = "Serial";
                salesSheet.Row(1).Style.Font.Bold = true;
                salesSheet.Row(1).Style.Fill.BackgroundColor = XLColor.LightGray;

                // Ejemplo/Instrucción
                currentRow++;
                salesSheet.Cell(currentRow, 1).Value = "IPH15PRO-256";
                salesSheet.Cell(currentRow, 2).Value = 1;
                salesSheet.Cell(currentRow, 3).Value = 5000000;
                salesSheet.Cell(currentRow, 4).Value = "123456789012345";
                salesSheet.Cell(currentRow, 5).Value = "";
                salesSheet.Row(currentRow).Style.Font.Italic = true;

                // --- Hoja 2: Lista de productos de referencia ---
                var productsSheet = workbook.Worksheets.Add("ListaProductos");
                var products = await _context.Products
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Name)
                    .AsNoTracking()
                    .ToListAsync();

                var productRow = 1;
                // Cabeceras
                productsSheet.Cell(productRow, 1).Value = "SKU";
                productsSheet.Cell(productRow, 2).Value = "Código";
                productsSheet.Cell(productRow, 3).Value = "Nombre";
                productsSheet.Cell(productRow, 4).Value = "Costo";
                productsSheet.Cell(productRow, 5).Value = "Precio Venta (Base)";
                productsSheet.Row(productRow).Style.Font.Bold = true;

                // Datos
                foreach (var product in products)
                {
                    productRow++;
                    productsSheet.Cell(productRow, 1).Value = product.SKU;
                    productsSheet.Cell(productRow, 2).Value = product.Code;
                    productsSheet.Cell(productRow, 3).Value = product.Name;
                    productsSheet.Cell(productRow, 4).Value = product.Cost;
                    productsSheet.Cell(productRow, 5).Value = product.Price;
                }

                // Ajustar columnas y proteger hoja
                salesSheet.Columns().AdjustToContents();
                productsSheet.Columns().AdjustToContents();
                productsSheet.Protect("phonepalace"); // Proteger con una contraseña simple

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(
                        content,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Plantilla_Ventas_{DateTime.Now:yyyyMMdd}.xlsx");
                }
            }
        }

        [HttpPost]
        [Route("EmitirElectronica/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EmitirFacturaElectronica(int id)
        {
            // 1. Obtener la venta y sus detalles
            var sale = await _context.Sales
                .Include(s => s.Client)
                .Include(s => s.Invoice)
                    .ThenInclude(i => i.Payments)
                .Include(s => s.Details)
                    .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(s => s.SaleID == id);

            if (sale == null || sale.Invoice == null)
            {
                return NotFound();
            }

            // 2. Validar si ya fue emitida consultando la entidad independiente
            var existingElectronic = await _context.Set<ElectronicInvoice>()
                .FirstOrDefaultAsync(e => e.InvoiceID == sale.Invoice.InvoiceID && e.Status == "Accepted");

            if (existingElectronic != null)
            {
                TempData["Warning"] = $"Esta venta ya tiene la factura electrónica {existingElectronic.DianNumber}.";
                return RedirectToAction(nameof(Details), new { id = sale.SaleID });
            }

            try
            {
                // 3. Lógica de integración con Plemsi
                var plemsiResponse = await _plemsiService.SendInvoiceAsync(sale);

                if (plemsiResponse.Success)
                {
                    // 4. Crear el registro en la entidad independiente
                    var electronicInvoice = new ElectronicInvoice
                    {
                        InvoiceID = sale.Invoice.InvoiceID,
                        CUFE = plemsiResponse.Cufe,
                        DianNumber = plemsiResponse.Number,
                        QRCodeUrl = plemsiResponse.QrUrl,
                        Status = "Accepted",
                        IssueDate = DateTime.Now
                    };

                    _context.Add(electronicInvoice);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Factura electrónica emitida y validada por la DIAN exitosamente.";
                }
                else
                {
                    TempData["Error"] = $"Error al emitir factura electrónica: {plemsiResponse.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al emitir factura electrónica: {ex.Message}";
            }

            return RedirectToAction(nameof(Details), new { id = sale.SaleID });
        }
    }
}
