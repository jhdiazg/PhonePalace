using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.Helpers;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace PhonePalace.Web.Controllers
{
    [Route("VerificacionTarjeta")]
    public class CreditCardVerificationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBankService _bankService;
        private readonly IAuditService _auditService;

        public CreditCardVerificationsController(ApplicationDbContext context, IBankService bankService, IAuditService auditService)
        {
            _context = context;
            _bankService = bankService;
            _auditService = auditService;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(int? pageNumber, int? pageSize)
        {
            var query = _context.CreditCardVerifications
                .Include(v => v.Sale)
                    .ThenInclude(s => s.Client)
                .Include(v => v.Bank)
                .Where(v => v.Status == VerificationStatus.Pending)
                .OrderByDescending(v => v.CreationDate);

            var model = await PaginatedList<CreditCardVerification>.CreateAsync(query.AsNoTracking(), pageNumber ?? 1, pageSize ?? 15);
            return View(model);
        }

        [HttpPost("Verify/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(int id)
        {
            var verification = await _context.CreditCardVerifications.FirstOrDefaultAsync(v => v.CreditCardVerificationID == id);
            if (verification == null || verification.Status != VerificationStatus.Pending)
            {
                TempData["Error"] = "La verificación no se encontró o ya fue procesada.";
                return RedirectToAction(nameof(Index));
            }

            var payment = await _context.Payments.FindAsync(verification.PaymentID);
            if (payment == null)
            {
                TempData["Error"] = "No se encontró el pago asociado a esta verificación.";
                return RedirectToAction(nameof(Index));
            }

            // Afectar el banco
            await _bankService.RegisterIncomeFromPaymentAsync(payment);

            // Actualizar estado de la verificación
            verification.Status = VerificationStatus.Verified;
            verification.VerificationDate = DateTime.Now;
            _context.Update(verification);

            await _context.SaveChangesAsync();
            await _auditService.LogAsync("VerificacionTarjeta", $"Verificó y aplicó el pago con tarjeta de crédito para la venta #{verification.SaleID} por un monto de {verification.Amount:C}.");

            TempData["Success"] = "Pago verificado y aplicado al banco correctamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Reject/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            var verification = await _context.CreditCardVerifications.FirstOrDefaultAsync(v => v.CreditCardVerificationID == id);
            if (verification == null || verification.Status != VerificationStatus.Pending)
            {
                TempData["Error"] = "La verificación no se encontró o ya fue procesada.";
                return RedirectToAction(nameof(Index));
            }

            verification.Status = VerificationStatus.Rejected;
            verification.VerificationDate = DateTime.Now;
            verification.VerificationNotes = string.IsNullOrEmpty(reason) ? "Rechazado por el usuario." : reason;
            _context.Update(verification);
            
            await _context.SaveChangesAsync();
            await _auditService.LogAsync("VerificacionTarjeta", $"Rechazó el pago con tarjeta de crédito para la venta #{verification.SaleID} por un monto de {verification.Amount:C}. Razón: {verification.VerificationNotes}");

            TempData["Warning"] = "El pago ha sido marcado como rechazado y no afectará al banco. Considere contactar al cliente.";
            return RedirectToAction(nameof(Index));
        }
    }
}
