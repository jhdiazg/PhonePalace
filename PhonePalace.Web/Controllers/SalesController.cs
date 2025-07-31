using Microsoft.AspNetCore.Mvc;
using PhonePalace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PhonePalace.Web.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using PhonePalace.Web.Helpers;
using PhonePalace.Domain.Enums;
using PhonePalace.Domain.Entities;
using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using PhonePalace.Web.Documents;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;

public class SalesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly ILogger<SalesController> _logger;

    public SalesController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, ILogger<SalesController> logger)
    {
        _context = context;
        _webHostEnvironment = webHostEnvironment;
        _logger = logger;
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    [Route("Ventas")]
    public async Task<IActionResult> Index()
    {
        var invoices = await _context.Invoices
            .Include(i => i.Client)
            .OrderByDescending(i => i.SaleDate)
            .ToListAsync();
        return View(invoices);
    }

    [Route("Ventas/Crear")]
    public async Task<IActionResult> Create()
    {
        var viewModel = new SaleCreateViewModel
        {
            Clients = new SelectList(await _context.Clients.ToListAsync(), "ClientID", "DisplayName"),
            Products = new SelectList(await _context.Products.Where(p => p.IsActive).ToListAsync(), "ProductID", "Name"),
            PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>()
        };
        return View(viewModel);
    }

    [HttpPost]
    [Route("Ventas/Crear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SaleCreateViewModel model)
    {
        // Método auxiliar para recargar los dropdowns en caso de error
        async Task PopulateViewModelDropdowns()
        {
            model.Clients = new SelectList(await _context.Clients.ToListAsync(), "ClientID", "DisplayName", model.ClientID);
            model.Products = new SelectList(await _context.Products.Where(p => p.IsActive).ToListAsync(), "ProductID", "Name");
            model.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>();
        }

        // 1. Limpieza y validaciones iniciales del modelo.
        model.Details.RemoveAll(d => d.ProductID == 0);
        if (!model.Details.Any())
        {
            ModelState.AddModelError("", "Debe agregar al menos un producto a la venta.");
        }

        // Si el modelo ya es inválido (por DataAnnotations o la validación anterior), retornar inmediatamente.
        if (!ModelState.IsValid)
        {
            await PopulateViewModelDropdowns();
            return View(model);
        }

        // Usar una transacción para garantizar la atomicidad de la operación.
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            decimal subtotal = 0;
            var invoiceDetails = new List<InvoiceDetail>();

            // 2. Validaciones de lógica de negocio (ej. Stock).
            // Se realizan todas las validaciones ANTES de modificar la base de datos.
            foreach (var detailVm in model.Details)
            {
                var product = await _context.Products
                    .Include(p => p.InventoryLevels)
                    .FirstOrDefaultAsync(p => p.ProductID == detailVm.ProductID);

                if (product == null)
                {
                    ModelState.AddModelError("", $"El producto con ID {detailVm.ProductID} no fue encontrado.");
                    continue; // Continúa validando los demás productos.
                }

                var inventory = product.InventoryLevels.FirstOrDefault();
                if (inventory == null || inventory.Stock < detailVm.Quantity)
                {
                    ModelState.AddModelError("", $"No hay stock suficiente para el producto '{product.Name}'. Stock actual: {inventory?.Stock ?? 0}.");
                }
            }

            // Calcular totales para validar el pago.
            var taxRate = 0.19m; // 19% de IVA.
            subtotal = model.Details.Sum(d => d.Quantity * d.UnitPrice);
            var tax = subtotal * taxRate;
            var total = subtotal + tax;

            var totalPaid = model.Payments?.Sum(p => p.Amount) ?? 0;
            if (totalPaid < total)
            {
                ModelState.AddModelError("", $"El monto pagado ({totalPaid:C}) es menor al total de la factura ({total:C}).");
            }

            // Si hubo algún error de validación de negocio, no continuar.
            if (!ModelState.IsValid)
            {
                await transaction.RollbackAsync();
                await PopulateViewModelDropdowns();
                return View(model);
            }

            // 3. Procesar la venta si todo es válido.
            // Ahora sí se modifica la base de datos.
            foreach (var detailVm in model.Details)
            {
                var product = await _context.Products
                    .Include(p => p.InventoryLevels)
                    .FirstAsync(p => p.ProductID == detailVm.ProductID);

                var inventory = product.InventoryLevels.First();
                inventory.Stock -= detailVm.Quantity;
                inventory.LastUpdated = DateTime.UtcNow;

                invoiceDetails.Add(new InvoiceDetail
                {
                    ProductID = detailVm.ProductID,
                    Quantity = detailVm.Quantity,
                    UnitPrice = product.Price // Usar el precio de la BD para seguridad.
                });
            }

            var invoice = new Invoice
            {
                ClientID = model.ClientID,
                SaleDate = model.SaleDate,
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "System",
                Subtotal = invoiceDetails.Sum(d => d.LineTotal), // Recalcular con datos seguros
                Tax = invoiceDetails.Sum(d => d.LineTotal) * taxRate,
                Total = invoiceDetails.Sum(d => d.LineTotal) * (1 + taxRate),
                Status = InvoiceStatus.Completed,
                Details = invoiceDetails
            };

            if (model.Payments != null)
            {
                invoice.Payments = model.Payments.Select(p => new Payment
                {
                    PaymentMethod = p.PaymentMethod,
                    Amount = p.Amount,
                    ReferenceNumber = p.ReferenceNumber
                }).ToList();
            }

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] = $"Venta #{invoice.InvoiceID} completada exitosamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error inesperado al procesar la venta para el cliente ID {ClientId}", model.ClientID);
            ModelState.AddModelError("", "Ocurrió un error inesperado al procesar la venta. Por favor, intente de nuevo.");
            await PopulateViewModelDropdowns();
            return View(model);
        }
    }

    [Route("Ventas/FacturaPdf/{id}")]
    public async Task<IActionResult> GenerateInvoicePdf(int id)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Client)
            .Include(i => i.Details)
                .ThenInclude(d => d.Product)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.InvoiceID == id);

        if (invoice == null)
        {
            return NotFound();
        }

        string wwwRootPath = _webHostEnvironment.WebRootPath;
        string logoPath = Path.Combine(wwwRootPath, "images", "logo_pdf.jpg");
        byte[] logoBytes = await System.IO.File.ReadAllBytesAsync(logoPath);

        var document = new InvoicePdfDocument(invoice, logoBytes);
        var pdfBytes = document.GeneratePdf();

        return File(pdfBytes, "application/pdf", $"Factura-{invoice.InvoiceID:D5}.pdf");
    }
}