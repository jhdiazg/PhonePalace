
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

namespace PhonePalace.Web.Controllers
{
    public class QuotesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public QuotesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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
            return View(viewModel);
        }

        // POST: Quotes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(QuoteCreateViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var client = await _context.Clients.FindAsync(viewModel.ClientID);
                if (client == null)
                {
                    ModelState.AddModelError("", $"Cliente con ID {viewModel.ClientID} no encontrado.");
                    viewModel.Clients = new SelectList(await _context.Clients.ToListAsync(), "ClientID", "DisplayName", viewModel.ClientID);
                    viewModel.Products = new SelectList(await _context.Products.Where(p => p.IsActive).ToListAsync(), "ProductID", "Name");
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

                foreach (var detailVm in viewModel.Details)
                {
                    var product = await _context.Products.FindAsync(detailVm.ProductID);
                    if (product == null)
                    {
                        ModelState.AddModelError("", $"Producto con ID {detailVm.ProductID} no encontrado.");
                        // Re-populate SelectLists before returning view
                        viewModel.Clients = new SelectList(await _context.Clients.ToListAsync(), "ClientID", "DisplayName", viewModel.ClientID);
                        viewModel.Products = new SelectList(await _context.Products.Where(p => p.IsActive).ToListAsync(), "ProductID", "Name");
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

                // Calculate Total (Subtotal + Tax) - Assuming 0 tax for now
                quote.Subtotal = quote.Details.Sum(d => d.Quantity * d.UnitPrice);
                quote.Tax = 0; // Implement tax calculation if needed
                quote.Total = quote.Subtotal + quote.Tax;

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
            return View(viewModel);
        }

        // GET: Quotes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var quote = await _context.Quotes.FindAsync(id);
            if (quote == null)
            {
                return NotFound();
            }
            ViewData["ClientID"] = new SelectList(_context.Clients, "ClientID", "DisplayName", quote.ClientID);
            return View(quote);
        }

        // POST: Quotes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("QuoteID,QuoteDate,ClientID,Total")] Quote quote)
        {
            if (id != quote.QuoteID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(quote);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!QuoteExists(quote.QuoteID))
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
            ViewData["ClientID"] = new SelectList(_context.Clients, "ClientID", "DisplayName", quote.ClientID);
            return View(quote);
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
    }
}
