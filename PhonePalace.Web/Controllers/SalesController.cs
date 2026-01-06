
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.Helpers;
using System.Threading.Tasks;
using System.Linq;

namespace PhonePalace.Web.Controllers
{
    [Route("Ventas")]
    public class SalesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SalesController(ApplicationDbContext context)
        {
            _context = context;
        }

    [HttpGet]
    [Route("")]
    public async Task<IActionResult> Index()
        {
            var sales = _context.Sales.Include(s => s.Client);
            return View(await sales.ToListAsync());
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
            decimal taxRate = 0.19m;
            if (viewModel.Details != null)
            {
                foreach (var detailVM in viewModel.Details)
                {
                    var product = await _context.Products.FindAsync(detailVM.ProductID);
                    if (product == null) continue;
                    decimal price = product.Price;
                    subtotal += detailVM.Quantity * price;
                    if (product.BillWithIVA) subtotalForVAT += detailVM.Quantity * price;
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
