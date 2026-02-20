using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhonePalace.Domain.Interfaces;
using System;
using System.Threading.Tasks;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Administrador")]
    [Route("Backup")]
    public class BackupController : Controller
    {
        private readonly IBackupService _backupService;
        private readonly IAuditService _auditService;

        public BackupController(IBackupService backupService, IAuditService auditService)
        {
            _backupService = backupService;
            _auditService = auditService;
        }

        [HttpGet("")]
        public IActionResult Index()
        {
            var backups = _backupService.GetBackups();
            return View(backups);
        }

        [HttpPost("Generar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create()
        {
            try
            {
                var path = await _backupService.CreateBackupAsync();
                await _auditService.LogAsync("Sistema", $"Backup de base de datos generado exitosamente: {path}");
                TempData["Success"] = $"Backup generado correctamente en el servidor: {path}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al generar backup: {ex.Message}";
            }

            // Redirigir a donde prefieras, por ejemplo al Dashboard o Configuración
            return RedirectToAction(nameof(Index));
        }
    }
}
