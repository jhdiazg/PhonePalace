﻿using Microsoft.AspNetCore.Http;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PhonePalace.Infrastructure.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string origin, string description)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return;

            var auditLog = new AuditLog
            {
                UserId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier),
                UserName = httpContext.User?.Identity?.Name,
                SessionId = httpContext.Session.Id,
                Timestamp = DateTime.UtcNow,
                Origin = origin,
                Description = description,
                IPAddress = httpContext.Connection?.RemoteIpAddress?.ToString()
            };

            await _context.AuditLogs.AddAsync(auditLog);
            await _context.SaveChangesAsync();
        }
    }
}