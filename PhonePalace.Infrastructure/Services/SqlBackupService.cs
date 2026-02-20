using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PhonePalace.Infrastructure.Services
{
    public class SqlBackupService : IBackupService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SqlBackupService> _logger;

        public SqlBackupService(ApplicationDbContext context, IConfiguration configuration, ILogger<SqlBackupService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> CreateBackupAsync()
        {
            var connectionString = _context.Database.GetConnectionString();
            var builder = new SqlConnectionStringBuilder(connectionString);
            var databaseName = builder.InitialCatalog;

            // Ruta donde se guardarán los backups. 
            // NOTA: El servicio de SQL Server debe tener permisos de escritura en esta carpeta.
            // Puedes configurar "BackupSettings:Path" en appsettings.json o usar esta ruta por defecto.
            var backupFolder = _configuration["BackupSettings:Path"] ?? @"C:\Backups\PhonePalace";

            // Intentamos crear el directorio desde la App (útil si están en el mismo servidor)
            if (!Directory.Exists(backupFolder))
            {
                try 
                {
                    Directory.CreateDirectory(backupFolder);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"No se pudo crear el directorio desde la App: {ex.Message}. Se asume que existe o que SQL Server tiene acceso.");
                }
            }

            var fileName = $"{databaseName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
            var fullPath = Path.Combine(backupFolder, fileName);

            // Comando T-SQL para backup
            var sql = $@"BACKUP DATABASE [{databaseName}] TO DISK = @path WITH FORMAT, INIT, NAME = @name, SKIP, NOREWIND, NOUNLOAD, STATS = 10";
            
            var pathParam = new SqlParameter("@path", fullPath);
            var nameParam = new SqlParameter("@name", $"{databaseName}-Full Database Backup");

            // Ejecutar comando raw
            await _context.Database.ExecuteSqlRawAsync(sql, pathParam, nameParam);

            _logger.LogInformation($"Backup creado exitosamente en: {fullPath}");
            return fullPath;
        }

        public IEnumerable<BackupFileInfo> GetBackups()
        {
            var backupFolder = _configuration["BackupSettings:Path"] ?? @"C:\Backups\PhonePalace";

            if (!Directory.Exists(backupFolder)) return new List<BackupFileInfo>();

            return new DirectoryInfo(backupFolder).GetFiles("*.bak")
                .OrderByDescending(f => f.CreationTime)
                .Select(f => new BackupFileInfo
                {
                    Name = f.Name,
                    FullPath = f.FullName,
                    CreationTime = f.CreationTime,
                    Size = f.Length
                });
        }
    }
}
