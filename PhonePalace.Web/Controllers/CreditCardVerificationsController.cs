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
                .Include(v => v.AccountReceivablePayment)
                    .ThenInclude(arp => arp.AccountReceivable)
                        .ThenInclude(ar => ar.Client)
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
            var verification = await _context.CreditCardVerifications
                .Include(v => v.AccountReceivablePayment)
                    .ThenInclude(arp => arp.AccountReceivable)
                .FirstOrDefaultAsync(v => v.CreditCardVerificationID == id);

            if (verification == null || verification.Status != VerificationStatus.Pending)
            {
                TempData["Error"] = "La verificación no se encontró o ya fue procesada.";
                return RedirectToAction(nameof(Index));
            }

            // Escenario 1: Verificación de Venta (PaymentID existe)
            if (verification.PaymentID.HasValue)
            {
                var payment = await _context.Payments.FindAsync(verification.PaymentID);
                if (payment == null)
                {
                    TempData["Error"] = "No se encontró el pago de venta asociado.";
                    return RedirectToAction(nameof(Index));
                }
                // Afectar el banco usando el servicio de pagos
                await _bankService.RegisterIncomeFromPaymentAsync(payment);
            }
            // Escenario 2: Verificación de Abono a CxC (AccountReceivablePaymentID existe)
            else if (verification.AccountReceivablePaymentID.HasValue && verification.AccountReceivablePayment != null)
            {
                // Registrar ingreso manual en el banco
                await _bankService.RegisterManualMovementAsync(
                    verification.BankID,
                    BankTransactionType.TransferIn, // Ingreso
                    verification.Amount,
                    $"Abono CxC #{verification.AccountReceivablePayment.AccountReceivableID} (TC Verificada) - Ref: {verification.CreditCardVerificationID}"
                );
            }
            else
            {
                TempData["Error"] = "La verificación no tiene un origen de pago válido.";
                return RedirectToAction(nameof(Index));
            }

            // Actualizar estado de la verificación
            verification.Status = VerificationStatus.Verified;
            verification.VerificationDate = DateTime.Now;
            _context.Update(verification);

            await _context.SaveChangesAsync();
            
            string sourceRef = verification.SaleID.HasValue ? $"Venta #{verification.SaleID}" : $"CxC #{verification.AccountReceivablePayment?.AccountReceivableID}";
            await _auditService.LogAsync("VerificacionTarjeta", $"Verificó y aplicó pago TC para {sourceRef} por {verification.Amount:C}.");

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
