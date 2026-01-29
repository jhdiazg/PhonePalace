
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace PhonePalace.Web.Controllers
{
    public class QuotesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public QuotesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IConfiguration config, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _config = config;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Quotes
        public async Task<IActionResult> Index()
        {
            var quotes = _context.Quotes.Include(q => q.Client);
            return View(await quotes.ToListAsync());
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
                .ThenInclude(qd => qd.Product)
                .FirstOrDefaultAsync(m => m.QuoteID == id);
            if (quote == null)
            {
                return NotFound();
            }

            var taxRate = _config.GetValue<decimal>("TaxSettings:IVARate");
            if (taxRate > 1) taxRate /= 100;
            ViewBag.TaxRate = taxRate;

            return View(quote);
        }

        // GET: Quotes/Create
        public async Task<IActionResult> Create()
        {
            var viewModel = new QuoteCreateViewModel
            {
                Clients = new SelectList(await _context.Clients.ToListAsync(), "ClientID", "DisplayName"),
                Products = new SelectList(await _context.Products.Where(p => p.IsActive).ToListAsync(), "ProductID", "Name"),
                QuoteDate = DateTime.Today,
                ExpirationDate = DateTime.Today.AddDays(7)
            };
            ViewBag.AllProducts = await _context.Products.Where(p => p.IsActive).Select(p => new { p.ProductID, p.Name, p.Price }).OrderBy(p => p.Name).ToListAsync();
            return View(viewModel);
        }

        // POST: Quotes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(QuoteCreateViewModel viewModel)
        {
            if (viewModel.Details == null || !viewModel.Details.Any())
            {
                ModelState.AddModelError("", "Debe agregar al menos un producto a la cotización.");
            }

            if (ModelState.IsValid)
            {
                var client = await _context.Clients.FindAsync(viewModel.ClientID);
                if (client == null)
                {
                    ModelState.AddModelError("", $"Cliente con ID {viewModel.ClientID} no encontrado.");
                    viewModel.Clients = new SelectList(await _context.Clients.ToListAsync(), "ClientID", "DisplayName", viewModel.ClientID);
                    viewModel.Products = new SelectList(await _context.Products.Where(p => p.IsActive).ToListAsync(), "ProductID", "Name");
                    ViewBag.AllProducts = await _context.Products.Where(p => p.IsActive).Select(p => new { p.ProductID, p.Name, p.Price }).OrderBy(p => p.Name).ToListAsync();
                    return View(viewModel);
                }

                var quote = new Quote
                {
                    QuoteDate = viewModel.QuoteDate,
                    ExpirationDate = viewModel.ExpirationDate,
                    ClientID = viewModel.ClientID,
                    Client = client, // Set the required Client navigation property
                    Status = "Pending", // Default status
                    Details = new List<QuoteDetail>()
                };

                foreach (var detailVm in viewModel.Details ?? new List<QuoteDetailViewModel>())
                {
                    var product = await _context.Products.FindAsync(detailVm.ProductID);
                    if (product == null)
                    {
                        ModelState.AddModelError("", $"Producto con ID {detailVm.ProductID} no encontrado.");
                        // Re-populate SelectLists before returning view
                        viewModel.Clients = new SelectList(await _context.Clients.ToListAsync(), "ClientID", "DisplayName", viewModel.ClientID);
                        viewModel.Products = new SelectList(await _context.Products.Where(p => p.IsActive).ToListAsync(), "ProductID", "Name");
                        ViewBag.AllProducts = await _context.Products.Where(p => p.IsActive).Select(p => new { p.ProductID, p.Name, p.Price }).OrderBy(p => p.Name).ToListAsync();
                        return View(viewModel);
                    }

                    quote.Details.Add(new QuoteDetail
                    {
                        Quote = quote, // Set the required Quote navigation property
                        Product = product, // Set the required Product navigation property
                        ProductID = detailVm.ProductID,
                        Quantity = detailVm.Quantity,
                        UnitPrice = detailVm.UnitPrice
                    });
                }

                // Calculate Total (Subtotal + Tax)
                var taxRate = _config.GetValue<decimal>("TaxSettings:IVARate");
                if (taxRate > 1) taxRate /= 100;

                // Asumimos que el precio unitario ya incluye IVA (igual que en Ventas)
                quote.Total = quote.Details.Sum(d => d.Quantity * d.UnitPrice);
                quote.Subtotal = quote.Total / (1 + taxRate);
                quote.Tax = quote.Total - quote.Subtotal;

                _context.Add(quote);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // If ModelState is not valid, re-populate SelectLists and return view
            foreach (var modelStateEntry in ModelState.Values)
            {
                foreach (var error in modelStateEntry.Errors)
                {
                    Console.WriteLine($"Error: {error.ErrorMessage}");
                }
            }

            viewModel.Clients = new SelectList(await _context.Clients.ToListAsync(), "ClientID", "DisplayName", viewModel.ClientID);
            viewModel.Products = new SelectList(await _context.Products.Where(p => p.IsActive).ToListAsync(), "ProductID", "Name");
            ViewBag.AllProducts = await _context.Products.Where(p => p.IsActive).Select(p => new { p.ProductID, p.Name, p.Price }).OrderBy(p => p.Name).ToListAsync();
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
                Status = quote.Status ?? "Pending",
                Details = quote.Details.Select(d => new QuoteDetailViewModel 
                {
                    ProductID = d.ProductID,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice
                }).ToList(),
                Clients = new SelectList(await _context.Clients.ToListAsync(), "ClientID", "DisplayName", quote.ClientID),
                Products = new SelectList(await _context.Products.Where(p => p.IsActive).ToListAsync(), "ProductID", "Name")
            };

            ViewBag.AllProducts = await _context.Products.Where(p => p.IsActive).Select(p => new { p.ProductID, p.Name, p.Price }).OrderBy(p => p.Name).ToListAsync();
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

            if (ModelState.IsValid)
            {
                try
                {
                    var quote = await _context.Quotes.Include(q => q.Details).FirstOrDefaultAsync(q => q.QuoteID == id);
                    if (quote == null) return NotFound();

                    quote.ClientID = viewModel.ClientID;
                    quote.QuoteDate = viewModel.QuoteDate;
                    quote.ExpirationDate = viewModel.ExpirationDate;

                    // Actualizar detalles: Limpiar existentes y agregar nuevos
                    quote.Details.Clear();
                    if (viewModel.Details != null)
                    {
                        foreach (var detail in viewModel.Details)
                        {
                            var product = await _context.Products.FindAsync(detail.ProductID);
                            if (product != null)
                            {
                                quote.Details.Add(new QuoteDetail
                                {
                                    QuoteID = quote.QuoteID,
                                    Quote = quote,
                                    ProductID = detail.ProductID,
                                    Quantity = detail.Quantity,
                                    UnitPrice = detail.UnitPrice,
                                    Product = product
                                });
                            }
                        }
                    }

                    // Recalcular totales
                    var taxRate = _config.GetValue<decimal>("TaxSettings:IVARate");
                    if (taxRate > 1) taxRate /= 100;

                    quote.Total = quote.Details.Sum(d => d.Quantity * d.UnitPrice);
                    quote.Subtotal = quote.Total / (1 + taxRate);
                    quote.Tax = quote.Total - quote.Subtotal;

                    _context.Update(quote);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!QuoteExists(viewModel.QuoteID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            
            viewModel.Clients = new SelectList(await _context.Clients.ToListAsync(), "ClientID", "DisplayName", viewModel.ClientID);
            viewModel.Products = new SelectList(await _context.Products.Where(p => p.IsActive).ToListAsync(), "ProductID", "Name");
            ViewBag.AllProducts = await _context.Products.Where(p => p.IsActive).Select(p => new { p.ProductID, p.Name, p.Price }).OrderBy(p => p.Name).ToListAsync();
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

        private bool QuoteExists(int id)
        {
            return _context.Quotes.Any(e => e.QuoteID == id);
        }

        // POST: Quotes/ConvertToSale/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConvertToSale(int id)
        {
            var quote = await _context.Quotes
                .Include(q => q.Details)
                .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(q => q.QuoteID == id);

            if (quote == null)
            {
                return NotFound();
            }

            var client = await _context.Clients.FindAsync(quote.ClientID);
            if (client == null)
            {
                return NotFound();
            }

            // Construye el SaleCreateViewModel para la venta
            var saleCreateViewModel = new PhonePalace.Web.ViewModels.SaleCreateViewModel
            {
                ClientID = quote.ClientID,
                SaleDate = DateTime.Now,
                SaleChannel = Domain.Enums.SaleChannel.Quotations,
                Details = quote.Details.Select(qd => new PhonePalace.Web.ViewModels.SaleDetailViewModel
                {
                    ProductID = qd.ProductID,
                    Quantity = qd.Quantity
                }).ToList(),
                Payments = new List<PhonePalace.Web.ViewModels.PaymentViewModel>()
            };

            TempData["SaleModel"] = System.Text.Json.JsonSerializer.Serialize(saleCreateViewModel);
            TempData["InfoMessage"] = "Cotización convertida. Completa la venta y registra los pagos.";
            return RedirectToAction("Create", "Sales");
        }

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

        [HttpGet]
        public async Task<IActionResult> GeneratePdf(int id)
        {
            var quote = await _context.Quotes
                .Include(q => q.Client)
                .Include(q => q.Details)
                .ThenInclude(qd => qd.Product)
                .FirstOrDefaultAsync(m => m.QuoteID == id);

            if (quote == null)
            {
                return NotFound();
            }

            string wwwRootPath = _webHostEnvironment.WebRootPath;
            string logoPath = Path.Combine(wwwRootPath, "images", "Logo_pdf.jpg");
            byte[] logoBytes = Array.Empty<byte>();

            if (System.IO.File.Exists(logoPath))
            {
                logoBytes = await System.IO.File.ReadAllBytesAsync(logoPath);
            }

            var pdfGenerator = new PhonePalace.Web.Documents.QuotePdfDocument(quote, logoBytes);
            var pdfBytes = pdfGenerator.GeneratePdf();

            return File(pdfBytes, "application/pdf", $"Cotizacion-{id}.pdf");
        }
    }
}
