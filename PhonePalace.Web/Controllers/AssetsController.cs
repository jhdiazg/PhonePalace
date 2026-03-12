using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Web.Helpers;
using System.Security.Claims;
using System;

namespace PhonePalace.Web.Controllers
{
    [Authorize]
    [Route("Activos")]
    public class AssetsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICashService _cashService;
        private readonly IAuditService _auditService;

        public AssetsController(ApplicationDbContext context, ICashService cashService, IAuditService auditService)
        {
            _context = context;
            _cashService = cashService;
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var assets = await _context.Assets
                .OrderByDescending(a => a.AcquisitionDate)
                .AsNoTracking()
                .ToListAsync();
            return View(assets);
        }

        [HttpGet("Create")]
        [Authorize(Roles = "Administrador,Cajero")]
        public IActionResult Create()
        {
            ViewBag.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>();
            return View();
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Cajero")]
        public async Task<IActionResult> Create(Asset asset, PaymentMethod? paymentMethod)
        {
            if (ModelState.IsValid)
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                try
                {
                    return await strategy.ExecuteAsync<IActionResult>(async () =>
                    {
                        using var transaction = await _context.Database.BeginTransactionAsync();
                        try
                        {
                            // Si se selecciona pago en Efectivo, registrar egreso de caja
                            if (paymentMethod == PaymentMethod.Cash)
                            {
                                var currentCash = await _cashService.GetCurrentCashRegisterAsync();
                                if (currentCash == null)
                                {
                                    throw new InvalidOperationException("Debe abrir la caja para registrar la compra en efectivo.");
                                }

                                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                                if (userId == null) return Unauthorized();
                                await _cashService.RegisterExpenseAsync(asset.AcquisitionCost, $"Compra de Activo: {asset.Name}", userId);
                            }

                            // Asegurar estado activo y fecha
                            asset.Status = AssetStatus.Active;
                            if (asset.AcquisitionDate == default) asset.AcquisitionDate = DateTime.Now;

                            _context.Assets.Add(asset);
                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();
                            return RedirectToAction(nameof(Index));
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }

            ViewBag.PaymentMethods = EnumHelper.ToSelectList<PaymentMethod>();
            return View(asset);
        }

        [HttpPost("DarDeBaja/{id?}")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Retire(int id)
        {
            var asset = await _context.Assets.FindAsync(id);
            if (asset == null) return NotFound();

            // Cambiar estado a Inactivo para que deje de sumar en el Balance Mensual
            asset.Status = AssetStatus.Decommissioned; 

            _context.Update(asset);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}