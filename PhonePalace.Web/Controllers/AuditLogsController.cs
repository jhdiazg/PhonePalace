﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Domain.Entities;
using PhonePalace.Web.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using PhonePalace.Web.Helpers;
using System.IO;

namespace PhonePalace.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AuditLogsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuditLogsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? selectedModule, string? searchUser, DateTime? startDate, DateTime? endDate, int? page)
        {
            // Define la página actual y el tamaño de la página
            var pageNumber = page ?? 1;
            var pageSize = 15; // Puedes ajustar este número según tus necesidades

            var query = _context.AuditLogs.AsQueryable();

            // Obtener lista de módulos (orígenes) para el dropdown
            var moduleList = await _context.AuditLogs
                .Select(log => log.Origin)
                .Distinct()
                .OrderBy(origin => origin)
                .Select(origin => new SelectListItem { Text = origin, Value = origin })
                .ToListAsync();

            // Aplicar filtro por módulo
            if (!string.IsNullOrEmpty(selectedModule))
            {
                query = query.Where(log => log.Origin == selectedModule);
            }

            // Aplicar los otros filtros
            if (!string.IsNullOrEmpty(searchUser))
            {
                query = query.Where(log => log.UserName != null && log.UserName.Contains(searchUser));
            }

            if (startDate.HasValue)
            {
                query = query.Where(log => log.Timestamp >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                query = query.Where(log => log.Timestamp < endDate.Value.Date.AddDays(1));
            }

            var viewModel = new AuditLogIndexViewModel
            {
                AuditLogs = await PaginatedList<AuditLog>.CreateAsync(query.OrderByDescending(log => log.Timestamp), pageNumber, pageSize),
                ModuleList = moduleList,
                SelectedModule = selectedModule,
                SearchUser = searchUser,
                StartDate = startDate,
                EndDate = endDate
            };

            return View(viewModel);
        }

        public async Task<IActionResult> ExportToExcel(string? selectedModule, string? searchUser, DateTime? startDate, DateTime? endDate)
        {
            var query = _context.AuditLogs.AsQueryable();

            // Replicamos la misma lógica de filtrado que en el método Index
            if (!string.IsNullOrEmpty(selectedModule))
            {
                query = query.Where(log => log.Origin == selectedModule);
            }
            if (!string.IsNullOrEmpty(searchUser))
            {
                query = query.Where(log => log.UserName != null && log.UserName.Contains(searchUser));
            }
            if (startDate.HasValue)
            {
                query = query.Where(log => log.Timestamp >= startDate.Value.Date);
            }
            if (endDate.HasValue)
            {
                query = query.Where(log => log.Timestamp < endDate.Value.Date.AddDays(1));
            }

            var auditLogs = await query.OrderByDescending(log => log.Timestamp).ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Auditoría");
                var currentRow = 1;

                // --- Cabeceras ---
                worksheet.Cell(currentRow, 1).Value = "Fecha y Hora (UTC)";
                worksheet.Cell(currentRow, 2).Value = "Usuario";
                worksheet.Cell(currentRow, 3).Value = "Módulo";
                worksheet.Cell(currentRow, 4).Value = "Descripción";
                worksheet.Cell(currentRow, 5).Value = "Dirección IP";
                worksheet.Row(1).Style.Font.Bold = true;

                // --- Datos ---
                foreach (var log in auditLogs)
                {
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = log.Timestamp;
                    worksheet.Cell(currentRow, 2).Value = log.UserName;
                    worksheet.Cell(currentRow, 3).Value = log.Origin;
                    worksheet.Cell(currentRow, 4).Value = log.Description;
                    worksheet.Cell(currentRow, 5).Value = log.IPAddress;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(
                        content,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"AuditLogs_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                }
            }
        }
    }
}
