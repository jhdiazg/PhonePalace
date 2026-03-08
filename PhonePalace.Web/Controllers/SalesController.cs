using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhonePalace.Domain.DTOs;
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
using Microsoft.Extensions.Logging;

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
        private readonly ILogger<SalesController> _logger;
        private readonly ISalesService _salesService; // Inyección del nuevo servicio

        public SalesController(ApplicationDbContext context, ICashService cashService, IBankService bankService, IConfiguration config, IWebHostEnvironment webHostEnvironment, IOptions<CompanySettings> companySettings, IPlemsiService plemsiService, IAuditService auditService, IEmailSender emailSender, ILogger<SalesController> logger, ISalesService salesService)
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
            _logger = logger;
            _salesService = salesService;
        }

    [HttpGet]
    [Route("")]
    public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, string clientName, int? pageNumber, int? pageSize)
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
            // Ajuste: Usar costo histórico (d.Cost) si existe, de lo contrario usar costo actual (para compatibilidad con datos antiguos)
            decimal totalProfit = await salesQuery.SelectMany(s => s.Details).SumAsync(d => d.Quantity * (d.UnitPrice - (d.Cost > 0 ? d.Cost : d.Product.Cost)));

            var sales = await PaginatedList<Sale>.CreateAsync(
                salesQuery.OrderByDescending(s => s.SaleDate).ThenByDescending(s => s.SaleID).AsNoTracking(), 
                pageNumber ?? 1, pageSize ?? 10);

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
                .IgnoreQueryFilters()
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

            // Pasar configuración para mostrar resolución en la vista
            ViewBag.CompanySettings = _companySettings;

            // Obtener el último consecutivo de factura electrónica para mostrar el siguiente.
            // Se hace al final para no interferir con otras lógicas.
            var lastIdFromTable = await _context.Set<ElectronicInvoice>()
                .OrderByDescending(e => e.ElectronicInvoiceID)
                .Select(e => e.ElectronicInvoiceID)
                .FirstOrDefaultAsync();

            // El método anterior (buscar el MAX(ID)) falla si se eliminan facturas, lo que crea "huecos" en la secuencia de identidad.
            // Esto es más común en producción.
            // La solución es consultar directamente a la base de datos por el valor de identidad actual de la tabla.
            // Esto es específico de SQL Server, pero es la forma más fiable de predecir el siguiente ID.
            long nextElectronicInvoiceNumber;
            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                var entityType = _context.Model.FindEntityType(typeof(ElectronicInvoice));
                var tableName = entityType!.GetTableName();

                command.CommandText = $"SELECT IDENT_CURRENT('[{tableName}]');";
                await _context.Database.OpenConnectionAsync();
                var result = await command.ExecuteScalarAsync();
                await _context.Database.CloseConnectionAsync();

                long lastIdentityValue = 0;
                if (result != null && result != DBNull.Value)
                {
                    lastIdentityValue = Convert.ToInt64(result);
                }

                // Si la tabla está vacía, IDENT_CURRENT devuelve el valor semilla (ej. 1). El próximo ID será ese valor.
                // Si la tabla tiene registros, IDENT_CURRENT devuelve el último ID usado. El próximo será ese valor + 1.
                nextElectronicInvoiceNumber = (lastIdFromTable == 0) ? lastIdentityValue : lastIdentityValue + 1;
            }

            // Asegurarse de que el número consecutivo no sea menor que el número inicial de la resolución.
            if (nextElectronicInvoiceNumber < _companySettings.DianResolutionStartNumber)
            {
                nextElectronicInvoiceNumber = _companySettings.DianResolutionStartNumber;
            }

            ViewBag.NextElectronicInvoiceNumber = nextElectronicInvoiceNumber;

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

                // RESTAURACIÓN: Validar que el precio no sea inferior al costo
                if (product != null && detailVM.UnitPrice < product.Cost)
                {
                    ModelState.AddModelError("", $"El precio del producto '{product.Name}' no puede ser inferior al costo ({product.Cost:C}).");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            // Delegar la lógica compleja al servicio de aplicación
            var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.Identity?.Name;
            
            // Mapeo de ViewModel a DTO
            var saleDto = new SaleCreateDto
            {
                ClientID = viewModel.ClientID,
                SaleDate = viewModel.SaleDate,
                SaleChannel = viewModel.SaleChannel,
                Details = viewModel.Details?.Select(d => new SaleDetailDto
                {
                    ProductID = d.ProductID,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    IMEI = d.IMEI,
                    Serial = d.Serial
                }).ToList() ?? new List<SaleDetailDto>(),
                Payments = viewModel.Payments?.Select(p => new PaymentDto
                {
                    PaymentMethod = p.PaymentMethod,
                    Amount = p.Amount,
                    ReferenceNumber = p.ReferenceNumber,
                    BankID = p.BankID
                }).ToList() ?? new List<PaymentDto>()
            };

            var result = await _salesService.ProcessSaleAsync(saleDto, userId!);

            if (result.Success)
            {
                return RedirectToAction(nameof(Index));
            }
            else
            {
                ModelState.AddModelError("", $"Error al procesar la venta: {result.ErrorMessage}");
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

        [HttpGet]
        [Route("GetClientBalance/{clientId}")]
        public async Task<IActionResult> GetClientBalance(int clientId)
        {
            var client = await _context.Clients
                .AsNoTracking()
                .Select(c => new { c.ClientID, c.Balance })
                .FirstOrDefaultAsync(c => c.ClientID == clientId);

            if (client == null) return NotFound();
            return Json(new { balance = client.Balance });
        }

        [HttpPost, ActionName("Delete")]
        [Route("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, string? cancellationReason)
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
                        // 0. Validar motivo de anulación si hay factura electrónica
                        var electronicInvoice = await _context.Set<ElectronicInvoice>()
                            .AsNoTracking() // No se necesita rastrear para esta validación
                            .FirstOrDefaultAsync(e => e.InvoiceID == sale.Invoice.InvoiceID && e.Status == "Accepted");

                        if (electronicInvoice != null && string.IsNullOrWhiteSpace(cancellationReason))
                        {
                            TempData["Error"] = "El motivo de anulación es obligatorio cuando existe una factura electrónica emitida.";
                            return RedirectToAction(nameof(Details), new { id });
                        }

                        // 1. Restaurar inventario
                        foreach (var detail in sale.Details)
                        {
                            var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductID == detail.ProductID);
                            if (inventory != null)
                            {
                                inventory.Stock += detail.Quantity;
                                inventory.LastUpdated = DateTime.Now;
                                _context.Update(inventory);

                                // Kardex: Entrada por Anulación
                                _context.Set<InventoryMovement>().Add(new InventoryMovement
                                {
                                    ProductId = detail.ProductID,
                                    Date = DateTime.Now,
                                    Type = InventoryMovementType.SaleCancellation,
                                    Quantity = detail.Quantity, // Positivo
                                    UnitCost = detail.Cost,
                                    StockBalance = (int)inventory.Stock,
                                    Reference = $"Anulación Venta #{sale.Invoice.InvoiceID}",
                                    UserId = User.Identity?.Name
                                });
                            }
                            else
                            {
                                // Si no existe registro de inventario (caso raro), lo creamos para no perder el stock
                                _context.Inventories.Add(new Inventory 
                                { 
                                    ProductID = detail.ProductID, 
                                    Stock = detail.Quantity, 
                                    LastUpdated = DateTime.Now 
                                });
                            }
                        }

                        // 2. Reversar Dinero (Caja y Bancos)
                        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                        if (sale.Invoice != null && sale.Invoice.Payments != null)
                        {
                            foreach (var payment in sale.Invoice.Payments)
                            {
                                // No reembolsar métodos que representan deuda (Crédito)
                                if (payment.PaymentMethod == PaymentMethod.Credit) continue;

                                // En lugar de devolver el dinero (Gasto), se genera un saldo a favor del cliente
                                if (sale.Client != null)
                                {
                                    sale.Client.Balance += payment.Amount;
                                    _context.Update(sale.Client);
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
                        if (electronicInvoice != null)
                        {
                            try 
                            {
                                string reason = cancellationReason!; // La validación anterior asegura que no es nulo
                                var ncResponse = await _plemsiService.SendCreditNoteAsync(sale, reason, electronicInvoice.CUFE, electronicInvoice.ElectronicInvoiceID);
                                if (ncResponse.Success)
                                {
                                    await _auditService.LogAsync("Facturación", $"Nota Crédito emitida para factura {sale.Invoice!.InvoiceID}. Número: {ncResponse.Number}");
                                }
                                else
                                {
                                    await _auditService.LogAsync("Error Facturación", $"Fallo al emitir Nota Crédito para {sale.Invoice!.InvoiceID}: {ncResponse.ErrorMessage}");
                                }
                            }
                            catch (Exception ex) 
                            { 
                                await _auditService.LogAsync("Error Facturación", $"Excepción al emitir Nota Crédito para {sale.Invoice!.InvoiceID}: {ex.Message}");
                            }
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
                .IgnoreQueryFilters()
                .Include(s => s.Client)
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

            // 3. Obtener Logo
            string wwwRootPath = _webHostEnvironment.WebRootPath;
            string logoPath = Path.Combine(wwwRootPath, _companySettings.LogoFacturaPath);
            byte[] logoBytes = Array.Empty<byte>();

            if (System.IO.File.Exists(logoPath))
            {
                logoBytes = await System.IO.File.ReadAllBytesAsync(logoPath);
            }

            string sellerName = sale.Invoice.UserId ?? "N/A";
            // 4. Generar PDF usando la configuración centralizada (_companySettings)
            var document = new InvoicePdfDocument(sale, logoBytes, _companySettings, sellerName);
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

            // CORRECCIÓN: Usar estrategia de ejecución para soportar reintentos (EnableRetryOnFailure)
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // --- INICIO: Envolver en una transacción para garantizar atomicidad ---
                // Esto asegura que o todo el proceso es exitoso, o no se deja ningún registro temporal en la base de datos.
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 3. Crear un registro de factura electrónica para obtener el consecutivo.
                    var electronicInvoice = new ElectronicInvoice
                    {
                        InvoiceID = sale.Invoice.InvoiceID,
                        Status = "Pending", // Estado inicial
                        IssueDate = DateTime.Now,
                        // Asignar valores temporales para campos que podrían ser no nulos en la BD.
                        // Esto previene un DbUpdateException si las columnas no permiten nulos.
                        CUFE = "PENDIENTE",
                        DianNumber = "PENDIENTE",
                        QRCodeUrl = ""
                    };
                    _context.Add(electronicInvoice);
                    await _context.SaveChangesAsync(); // Guardar para obtener el ID que será el consecutivo.

                    // 4. Lógica de integración con Plemsi, pasando el nuevo consecutivo
                    // El ID autoincremental de la tabla ElectronicInvoices se usa como número de factura.
                    var plemsiResponse = await _plemsiService.SendInvoiceAsync(sale, electronicInvoice.ElectronicInvoiceID);

                    if (plemsiResponse.Success)
                    {
                        // 5. Actualizar el registro de factura electrónica con los datos de la DIAN
                        electronicInvoice.CUFE = plemsiResponse.Cufe!;
                        electronicInvoice.DianNumber = plemsiResponse.Number!;
                        electronicInvoice.QRCodeUrl = plemsiResponse.QrUrl!;
                        electronicInvoice.Status = "Accepted";

                        _context.Update(electronicInvoice);
                        await _context.SaveChangesAsync();

                        // Si todo fue exitoso, confirmar la transacción
                        await transaction.CommitAsync();

                        await _auditService.LogAsync("Facturación", $"Emitió factura electrónica para venta #{sale.Invoice.InvoiceID}. DIAN: {electronicInvoice.DianNumber}");
                        TempData["Success"] = "Factura electrónica emitida y validada por la DIAN exitosamente.";
                    }
                    else
                    {
                        // Si la emisión falla, revertir la transacción. El registro 'PENDIENTE' no se guardará.
                        await transaction.RollbackAsync();
                        TempData["Error"] = $"Error al emitir factura electrónica: {plemsiResponse.ErrorMessage}";
                    }
                }
                catch (Exception ex)
                {
                    // Si cualquier parte del proceso falla (ej. conexión a BD), revertir la transacción.
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error durante la emisión de factura electrónica para la venta {SaleID}", id);
                    TempData["Error"] = $"Error al emitir factura electrónica: {ex.Message}";
                }
            });
            // --- FIN: Envolver en una transacción ---

            return RedirectToAction(nameof(Details), new { id = sale.SaleID });
        }

        [HttpPost("SincronizarElectronica/{electronicInvoiceId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SincronizarFacturaElectronica(int electronicInvoiceId)
        {
            // 1. Buscar el registro de factura local que falló.
            var electronicInvoice = await _context.Set<ElectronicInvoice>()
                .Include(e => e.Invoice)
                .FirstOrDefaultAsync(e => e.ElectronicInvoiceID == electronicInvoiceId);

            if (electronicInvoice == null || electronicInvoice.Invoice == null)
            {
                TempData["Error"] = "No se encontró el registro de factura a sincronizar.";
                return RedirectToAction(nameof(Index));
            }

            // CORRECCIÓN: Recuperar la venta asociada a la factura para obtener el SaleID
            // La entidad Invoice no tiene SaleID, por lo que buscamos en la tabla Sales por InvoiceID.
            var sale = await _context.Sales.AsNoTracking().FirstOrDefaultAsync(s => s.InvoiceID == electronicInvoice.InvoiceID);
            if (sale == null)
            {
                TempData["Error"] = "No se encontró la venta asociada a la factura.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // 2. Llamar al servicio que consulta el estado en Plemsi.
                var plemsiResponse = await _plemsiService.GetInvoiceStatusAsync(electronicInvoice.ElectronicInvoiceID);

                if (plemsiResponse.Success)
                {
                    // 3. Si se recuperan los datos, actualizar el registro local.
                    electronicInvoice.CUFE = plemsiResponse.Cufe!;
                    electronicInvoice.DianNumber = plemsiResponse.Number!;
                    electronicInvoice.QRCodeUrl = plemsiResponse.QrUrl!;
                    electronicInvoice.Status = "Accepted";

                    _context.Update(electronicInvoice);
                    await _context.SaveChangesAsync();

                    await _auditService.LogAsync("Facturación", $"Sincronización exitosa para venta #{sale.SaleID}. DIAN: {electronicInvoice.DianNumber}");
                    TempData["Success"] = "Factura sincronizada y actualizada exitosamente.";
                }
                else
                {
                    TempData["Error"] = $"Error al sincronizar: {plemsiResponse.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la sincronización de factura electrónica {ElectronicInvoiceID}", electronicInvoiceId);
                TempData["Error"] = $"Error de conexión al sincronizar: {ex.Message}";
            }

            // Devolver al usuario a la página de detalles de la venta usando el ID recuperado.
            return RedirectToAction(nameof(Details), new { id = sale.SaleID });
        }

        [HttpPost("ReintentarNotaCredito/{id}")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> RetryCreditNote(int id, string reason)
        {
            var sale = await _context.Sales
                .Include(s => s.Client)
                .Include(s => s.Invoice)
                .Include(s => s.Details).ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(s => s.SaleID == id);

            if (sale == null || sale.Invoice == null) return NotFound();

            var electronicInvoice = await _context.Set<ElectronicInvoice>()
                .FirstOrDefaultAsync(e => e.InvoiceID == sale.Invoice.InvoiceID && e.Status == "Accepted");

            if (electronicInvoice == null)
            {
                TempData["Error"] = "No existe una factura electrónica aprobada para esta venta.";
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                string finalReason = string.IsNullOrWhiteSpace(reason) ? "Anulación de venta (Reintento)" : reason;
                var ncResponse = await _plemsiService.SendCreditNoteAsync(sale, finalReason, electronicInvoice.CUFE, electronicInvoice.ElectronicInvoiceID);
                
                if (ncResponse.Success)
                {
                    await _auditService.LogAsync("Facturación", $"Nota Crédito emitida (Reintento) para factura {sale.Invoice.InvoiceID}. Número: {ncResponse.Number}");
                    TempData["Success"] = $"Nota Crédito {ncResponse.Number} emitida exitosamente en la DIAN.";
                }
                else
                {
                    TempData["Error"] = $"Error al emitir Nota Crédito: {ncResponse.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Excepción al emitir Nota Crédito: {ex.Message}";
            }

            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
