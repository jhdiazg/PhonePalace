using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.ViewModels;
using PhonePalace.Web.Documents;
using QuestPDF.Fluent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

public class QuotesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly EmailService _emailService;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public QuotesController(ApplicationDbContext context, EmailService emailService, IWebHostEnvironment webHostEnvironment)
    {
        _context = context;
        _emailService = emailService;
        _webHostEnvironment = webHostEnvironment;
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    // GET: Quotes
    public async Task<IActionResult> Index()
    {
        var quotes = await _context.Quotes
            .Include(q => q.Client)
            .OrderByDescending(q => q.QuoteDate)
            .ToListAsync();
        return View(quotes);
    }

    // GET: Quotes/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var quote = await _context.Quotes
            .Include(q => q.Client)
            .Include(q => q.Details)
                .ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(m => m.QuoteID == id);

        if (quote == null)
        {
            return NotFound();
        }

        return View(quote);
    }

    [HttpPost]
    public async Task<IActionResult> SendQuoteByEmail(int id)
    {
        var quote = await _context.Quotes
            .Include(q => q.Client)
            .Include(q => q.Details)
                .ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(q => q.QuoteID == id);

        if (quote == null)
        {
            return NotFound();
        }

        // 1. Generar el PDF en memoria
        string wwwRootPath = _webHostEnvironment.WebRootPath;
        string logoPath = Path.Combine(wwwRootPath, "images", "logo_pdf.jpg");
        byte[] logoBytes = await System.IO.File.ReadAllBytesAsync(logoPath);

        var document = new QuotePdfDocument(quote, logoBytes);
        byte[] pdfBytes = document.GeneratePdf();
        var fileName = $"Cotizacion-{quote.QuoteID}.pdf";

        // 2. Enviar el correo
        var subject = $"Cotización de PhonePalace #{quote.QuoteID}";
        var body = $"<p>Hola {(quote.Client != null ? quote.Client.DisplayName : "Cliente")},</p><p>Adjunto encontrarás la cotización solicitada.</p><p>Gracias,<br/>El equipo de PhonePalace</p>";

        if (quote.Client != null && !string.IsNullOrEmpty(quote.Client.Email))
        {
            await _emailService.SendEmailWithAttachmentAsync(quote.Client.Email, subject, body, pdfBytes, fileName);
        }
        else
        {
            // Log the error or handle the case where the client or email is missing
            Console.WriteLine("No se puede enviar el correo electrónico porque el cliente o el correo electrónico no están presentes.");
        }

        // TempData["SuccessMessage"] = "La cotización ha sido enviada por correo.";
        return RedirectToAction("Details", new { id = quote.QuoteID });
    }

    // GET: Quotes/Create
    public async Task<IActionResult> Create()
    {
        var viewModel = new QuoteCreateViewModel();
        await PopulateCreateDropdowns(viewModel);
        return View(viewModel);
    }

    // POST: Quotes/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(QuoteCreateViewModel viewModel)
    {
        // Remove la fila de plantilla vacía si se envía
        viewModel.Details.RemoveAll(d => d.ProductID == 0 || d.Quantity <= 0);

        if (!viewModel.Details.Any())
        {
            ModelState.AddModelError("", "Debe agregar al menos un producto a la cotización.");
        }

        if (ModelState.IsValid)
        {
            var client = await _context.Clients.FindAsync(viewModel.ClientID);
            if (client == null)
            {
                ModelState.AddModelError(string.Empty, "Cliente no encontrado.");
                await PopulateCreateDropdowns(viewModel);
                return View(viewModel);
            }

            var quote = new Quote
            {
                ClientID = viewModel.ClientID,
                QuoteDate = viewModel.QuoteDate,
                ExpirationDate = viewModel.ExpirationDate,
                Status = "Pending", // Estado inicial
                Client = client
            };

            decimal subtotal = 0;
            foreach (var detailVM in viewModel.Details)
            {
                var product = await _context.Products.FindAsync(detailVM.ProductID);
                if (product == null)
                {
                    ModelState.AddModelError(string.Empty, $"Product with ID {detailVM.ProductID} not found.");
                    await PopulateCreateDropdowns(viewModel);
                    return View(viewModel);
                }

                var detail = new QuoteDetail
                {
                    ProductID = detailVM.ProductID,
                    Quantity = detailVM.Quantity,
                    UnitPrice = detailVM.UnitPrice,
                    Product = product,
                    Quote = quote
                };
                quote.Subtotal += detail.Quantity * detail.UnitPrice;
                _context.QuoteDetails.Add(detail);
            }

            quote.Total = quote.Subtotal + quote.Tax;

            _context.Quotes.Add(quote);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        // Si el modelo no es válido, volver a la vista con los errores
        return View(viewModel);
    }

    // GET: Quotes/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var quote = await _context.Quotes
            .Include(q => q.Details)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.QuoteID == id);

        if (quote == null)
        {
            return NotFound();
        }

        var viewModel = new QuoteEditViewModel
        {
            QuoteID = quote.QuoteID,
            ClientID = quote.ClientID,
            QuoteDate = quote.QuoteDate,
            ExpirationDate = quote.ExpirationDate,
            Status = quote.Status,
            Details = quote.Details.Select(d => new QuoteDetailViewModel
            {
                ProductID = d.ProductID,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice
            }).ToList()
        };

        await PopulateEditDropdowns(viewModel);
        return View(viewModel);
    }

    // POST: Quotes/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, QuoteEditViewModel viewModel)
    {
        if (id != viewModel.QuoteID)
        {
            return NotFound();
        }

        viewModel.Details.RemoveAll(d => d.ProductID == 0 || d.Quantity <= 0);

        if (!viewModel.Details.Any())
        {
            ModelState.AddModelError("", "Debe agregar al menos un producto a la cotización.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                var quoteToUpdate = await _context.Quotes
                    .Include(q => q.Details)
                    .FirstOrDefaultAsync(q => q.QuoteID == id);

                if (quoteToUpdate == null)
                {
                    return NotFound();
                }

                // Actualizar propiedades
                quoteToUpdate.ClientID = viewModel.ClientID;
                quoteToUpdate.QuoteDate = viewModel.QuoteDate;
                quoteToUpdate.ExpirationDate = viewModel.ExpirationDate;
                quoteToUpdate.Status = viewModel.Status;

                // Actualizar detalles (método simple: borrar y volver a crear)
                _context.QuoteDetails.RemoveRange(quoteToUpdate.Details);

                decimal subtotal = 0;
                foreach (var detailVM in viewModel.Details)
                {
                    var product = await _context.Products.FindAsync(detailVM.ProductID);
                    if (product == null)
                    {
                        ModelState.AddModelError(string.Empty, $"Product with ID {detailVM.ProductID} not found.");
                        await PopulateEditDropdowns(viewModel);
                        return View(viewModel);
                    }

                    var detail = new QuoteDetail
                    {
                        ProductID = detailVM.ProductID,
                        Quantity = detailVM.Quantity,
                        Product = product,
                        UnitPrice = product.Price,
                        Quote = quoteToUpdate
                    };
                    quoteToUpdate.Details.Add(detail);
                    subtotal += detail.Quantity * detail.UnitPrice;
                }

                quoteToUpdate.Subtotal = subtotal;
                quoteToUpdate.Tax = quoteToUpdate.Subtotal * 0.15m;
                quoteToUpdate.Total = quoteToUpdate.Subtotal + quoteToUpdate.Tax;

                _context.Update(quoteToUpdate);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Quotes.Any(e => e.QuoteID == viewModel.QuoteID))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Details), new { id = viewModel.QuoteID });
        }

        await PopulateEditDropdowns(viewModel);
        return View(viewModel);
    }

    // GET: Quotes/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var quote = await _context.Quotes
            .Include(q => q.Client)
            .FirstOrDefaultAsync(m => m.QuoteID == id);

        if (quote == null)
        {
            return NotFound();
        }

        return View(quote);
    }

    // POST: Quotes/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var quote = await _context.Quotes.FindAsync(id);
        if (quote != null)
        {
            _context.Quotes.Remove(quote);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> GeneratePdf(int id)
    {
        var quote = await _context.Quotes
            .Include(q => q.Client)
            .Include(q => q.Details)
                .ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(q => q.QuoteID == id);

        if (quote == null)
        {
            return NotFound();
        }

        string wwwRootPath = _webHostEnvironment.WebRootPath;
        string logoPath = Path.Combine(wwwRootPath, "images", "logo_pdf.jpg");
        byte[] logoBytes = await System.IO.File.ReadAllBytesAsync(logoPath);

        var document = new QuotePdfDocument(quote, logoBytes);
        var pdfBytes = document.GeneratePdf();

        return File(pdfBytes, "application/pdf", $"Cotizacion-{quote.QuoteID}.pdf");
    }

    // GET: Quotes/GetProductPrice
    [HttpGet]
    public async Task<IActionResult> GetProductPrice(int productId)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null)
        {
            return NotFound();
        }
        return Json(new { price = product.Price });
    }

    private async Task PopulateCreateDropdowns(QuoteCreateViewModel viewModel)
    {
        var clients = await _context.Clients
            .OrderBy(c => c is LegalEntity ? ((LegalEntity)c).CompanyName : ((NaturalPerson)c).FirstName)
            .ThenBy(c => c is NaturalPerson ? ((NaturalPerson)c).LastName : null)
            .ToListAsync();
        viewModel.Clients = new SelectList(clients, "ClientID", "DisplayName", viewModel.ClientID);
        viewModel.Products = new SelectList(await _context.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(), "ProductID", "Name");
    }

    private async Task PopulateEditDropdowns(QuoteEditViewModel viewModel)
    {
        var clients = await _context.Clients
            .OrderBy(c => c is LegalEntity ? ((LegalEntity)c).CompanyName : ((NaturalPerson)c).FirstName)
            .ThenBy(c => c is NaturalPerson ? ((NaturalPerson)c).LastName : null)
            .ToListAsync();
        viewModel.Clients = new SelectList(clients, "ClientID", "DisplayName", viewModel.ClientID);
        viewModel.Products = new SelectList(await _context.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(), "ProductID", "Name");
        viewModel.Statuses = new SelectList(new List<string> { "Pending", "Approved", "Expired" }, viewModel.Status);
    }
}
