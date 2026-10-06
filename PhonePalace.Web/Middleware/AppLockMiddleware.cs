using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace PhonePalace.Web.Middleware
{
    public class AppLockMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _config;

        public AppLockMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            _config = config;
        }

        public Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            if (IsExempt(path))
            {
                return _next(context);
            }

            var feLocked = _config.GetValue<bool>("AppLock:FacturacionElectronica");
            var hsLocked = _config.GetValue<bool>("AppLock:HostingSoporte");

            if (feLocked || hsLocked)
            {
                context.Response.StatusCode = StatusCodes.Status302Found;
                context.Response.Headers.Location = "/Home/AppLocked";
                return Task.CompletedTask;
            }

            return _next(context);
        }

        private static bool IsExempt(string path)
        {
            return path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/images/", StringComparison.OrdinalIgnoreCase)
                || path.IndexOf("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("/Identity/Account/Logout", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("/Home/AppLocked", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}