
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.Helpers;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;

namespace PhonePalace.Web.Controllers
{
    [Route("Ventas")]
    public class SalesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICashService _cashService;
        private readonly IConfiguration _config;

        public SalesController(ApplicationDbContext context, ICashService cashService, IConfiguration config)
        {
            _context = context;
            _cashService = cashService;
            _config = config;
        }

    [HttpGet]
    [Route("")]
    public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, string? clientName)
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

            // Por defecto, mostrar solo ventas del día actual
            if (!startDate.HasValue && !endDate.HasValue && string.IsNullOrEmpty(clientName))
            {
                startDate = DateTime.Now.Date;
                endDate = DateTime.Now.Date;
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
                salesQuery = salesQuery.Where(s => s.Client.DisplayName.Contains(clientName));
            }

            var sales = await salesQuery.OrderByDescending(s => s.SaleDate).ToListAsync();

            // Calcular total de la consulta
            decimal total = sales.Sum(s => s.Invoice?.Total ?? 0);

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
    public IActionResult Create()
        {
            var viewModel = new ViewModels.SaleCreateViewModel();
            // Inicializa dropdowns
            viewModel.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>();
            viewModel.SaleChannels = new SelectList(EnumHelper.ToSelectList<SaleChannel>().Where(x => x.Value != "0"), "Value", "Text");
            viewModel.Clients = new SelectList(_context.Clients.Where(c => c.IsActive), "ClientID", "DisplayName");
            viewModel.Products = new SelectList(_context.Products.Where(p => p.IsActive), "ProductID", "Name");
            ViewBag.AllProducts = _context.Products.Where(p => p.IsActive).ToList();

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

            // Validaciones de caja
            var currentCash = await _cashService.GetCurrentCashRegisterAsync();
            if (currentCash == null)
            {
                ModelState.AddModelError("", "Debe abrir la caja antes de registrar ventas.");
            }

            // Validación de fecha: solo fecha actual permitida
            if (viewModel.SaleDate.Date != DateTime.Now.Date)
            {
                ModelState.AddModelError("SaleDate", "La fecha de venta debe ser la fecha actual.");
            }

            // Debug: Log payments
            TempData["Info"] = $"Payments: {string.Join(", ", viewModel.Payments?.Select(p => $"{p.PaymentMethod}:{p.Amount}") ?? new string[0])}";

            if (!ModelState.IsValid || viewModel.ClientID == null)
            {
                ModelState.AddModelError("ClientID", "Debe seleccionar un cliente.");
                return View(viewModel);
            }

            // Obtener el cliente
            var client = await _context.Clients.FindAsync(viewModel.ClientID.Value);
            if (client == null)
            {
                ModelState.AddModelError("ClientID", "Cliente no encontrado.");
                return View(viewModel);
            }

            // Calcular subtotal y IVA
            decimal subtotal = 0;
            decimal subtotalForVAT = 0;
            decimal taxRate = _config.GetValue<decimal>("TaxSettings:IVARate");
            if (viewModel.Details != null)
            {
                foreach (var detailVM in viewModel.Details)
                {
                    var product = await _context.Products.FindAsync(detailVM.ProductID);
                    if (product == null) continue;
                    decimal price = product.Price;
                    subtotal += detailVM.Quantity * price;
                    subtotalForVAT += detailVM.Quantity * price; // Todo se factura con IVA incluido
                }
            }
            decimal tax = subtotalForVAT * taxRate;
            decimal total = subtotal + tax;

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
                        ReferenceNumber = paymentVM.ReferenceNumber
                    };
                    payments.Add(payment);
                    _context.Payments.Add(payment);
                }
                await _context.SaveChangesAsync();

                // Registrar ingresos en caja para pagos en efectivo
                var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    foreach (var payment in payments.Where(p => p.PaymentMethod == PaymentMethod.Cash))
                    {
                        await _cashService.RegisterIncomeAsync(payment.Amount, $"Pago en efectivo por venta #{invoice.InvoiceID}", userId, payment.PaymentID);
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
                        UnitPrice = product.Price,
                        Sale = sale
                    };
                    sale.Details.Add(detail);
                }
            }

            _context.Sales.Add(sale);
            await _context.SaveChangesAsync();

            // Reducir inventario
            foreach (var detail in sale.Details)
            {
                var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductID == detail.ProductID);
                if (inventory != null && inventory.Stock >= detail.Quantity)
                {
                    inventory.Stock -= detail.Quantity;
                    _context.Update(inventory);
                }
            }
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
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
    }
}
