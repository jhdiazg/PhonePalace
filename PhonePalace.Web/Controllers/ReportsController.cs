using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.ViewModels;
using PhonePalace.Web.Helpers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PhonePalace.Web.Controllers
{
    [Authorize]
    [Route("Reportes")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("VentasPorFormaPago")]
        public async Task<IActionResult> SalesByPaymentMethod(DateTime? startDate, DateTime? endDate, string? selectedPaymentMethod, int? pageNumber, int? pageSize)
        {
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
    }
}