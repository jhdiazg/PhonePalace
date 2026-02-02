using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.ViewModels;
using PhonePalace.Web.Helpers;
using PhonePalace.Web.Documents;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace PhonePalace.Web.Controllers
{
    [Authorize]
    [Route("Reportes")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ReportsController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
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
                        where p.PaymentDate >= start && p.PaymentDate <= endFilter
                        select new
                        {
                            Payment = p,
                            Invoice = p.Invoice,
                            Client = p.Invoice!.Client ?? null,
                            SaleId = s != null ? s.SaleID : (int?)null
                        };

            if (!string.IsNullOrEmpty(selectedPaymentMethod))
            {
                if (Enum.TryParse<PhonePalace.Domain.Enums.PaymentMethod>(selectedPaymentMethod, out var paymentMethodEnum))
                {
                    query = query.Where(x => x.Payment.PaymentMethod == paymentMethodEnum);
                }
            }

            // 1. Calcular totales y resumen sobre toda la consulta (eficiente)
            var totalSales = await query.SumAsync(x => x.Payment.Amount);
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
                ClientName = x.Client!.DisplayName ?? "Cliente General",
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
                PaymentMethods = EnumHelper.ToSelectList<PhonePalace.Domain.Enums.PaymentMethod>().ToList(),
                TotalSales = totalSales,
                Summary = summary,
                Details = paginatedDetails
            };

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
    }
}