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

namespace PhonePalace.Web.Controllers
{
    [Route("Ventas")]
    public class SalesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICashService _cashService;
        private readonly IBankService _bankService;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly CompanySettings _companySettings;

        public SalesController(ApplicationDbContext context, ICashService cashService, IBankService bankService, IConfiguration config, IWebHostEnvironment webHostEnvironment, IOptions<CompanySettings> companySettings)
        {
            _context = context;
            _cashService = cashService;
            _bankService = bankService;
            _config = config;
            _webHostEnvironment = webHostEnvironment;
            _companySettings = companySettings.Value;
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

            // Por defecto, mostrar solo ventas del día actual
            if (!startDate.HasValue && !endDate.HasValue && string.IsNullOrEmpty(clientName))
            {
                // Si hay una caja abierta, mostrar ventas de esa fecha de apertura por defecto
                var currentCash = await _cashService.GetCurrentCashRegisterAsync();
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

            var sales = await PaginatedList<Sale>.CreateAsync(salesQuery.OrderByDescending(s => s.SaleDate).AsNoTracking(), pageNumber ?? 1, pageSize ?? 10);

            ViewData["StartDate"] = startDate?.ToString("yyyy-MM-dd");
            ViewData["EndDate"] = endDate?.ToString("yyyy-MM-dd");
            ViewData["ClientName"] = clientName;
            ViewData["Total"] = total;

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
            ViewBag.AllProducts = _context.Products.Where(p => p.IsActive).ToList();
            ViewBag.Banks = new SelectList(await _context.Banks.Where(b => b.IsActive).ToListAsync(), "BankID", "Name");
            
            var taxRate = _config.GetValue<decimal>("TaxSettings:IVARate");
            if (taxRate > 1) taxRate /= 100; // Normalizar si viene como entero (ej. 19 -> 0.19)
            ViewBag.TaxRate = taxRate;

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
            ViewBag.AllProducts = _context.Products.Where(p => p.IsActive).ToList();
            ViewBag.Banks = new SelectList(await _context.Banks.Where(b => b.IsActive).ToListAsync(), "BankID", "Name");
            
            var taxRate = _config.GetValue<decimal>("TaxSettings:IVARate");
            if (taxRate > 1) taxRate /= 100; // Normalizar
            ViewBag.TaxRate = taxRate;

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

            // Obtener el cliente
            var client = await _context.Clients.FindAsync(viewModel.ClientID!.Value);
            if (client == null)
            {
                ModelState.AddModelError("ClientID", "Cliente no encontrado.");
                return View(viewModel);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
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
                Status = InvoiceStatus.Completed
            };
            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            // Crear payments si hay
            if (viewModel.Payments != null && viewModel.Payments.Any())
            {
                var payments = new List<Payment>();
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

                // Registrar ingresos en caja para pagos en efectivo
                // Y registrar ingresos en bancos para los demás métodos de pago
                var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    foreach (var p in payments)
                    {
                        if (p.PaymentMethod == PaymentMethod.Cash)
                        {
                            await _cashService.RegisterIncomeAsync(p.Amount, $"Pago en efectivo por venta #{invoice.InvoiceID}", userId, p.PaymentID);
                        }
                        else if (p.BankID.HasValue) // Si es un pago bancario
                        {
                            await _bankService.RegisterIncomeFromPaymentAsync(p);
                        }
                    }
                }
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
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
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
            var sale = await _context.Sales
                .Include(s => s.Details)
                .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(s => s.SaleID == id);
            if (sale != null)
            {
                // Restaurar inventario
                foreach (var detail in sale.Details)
                {
                    var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductID == detail.ProductID);
                    if (inventory != null)
                    {
                        inventory.Stock += detail.Quantity;
                        _context.Update(inventory);
                    }
                }

                sale.IsDeleted = true;
                _context.Update(sale);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
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
    }
}
