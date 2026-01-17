using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using PhonePalace.Domain.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PhonePalace.Infrastructure.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _env;

        public FileStorageService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<string> SaveFileAsync(IFormFile file, string containerName)
        {
            var extension = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid()}{extension}";
            var folderPath = Path.Combine(_env.WebRootPath, containerName);

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var filePath = Path.Combine(folderPath, fileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            var relativePath = Path.Combine(containerName, fileName).Replace("\\", "/");
            return $"/{relativePath}";
        }

        public Task DeleteFileAsync(string route)
        {
            if (string.IsNullOrEmpty(route))
            {
                return Task.CompletedTask;
            }

            var relativePath = route.TrimStart('/');
            var filePath = Path.Combine(_env.WebRootPath, relativePath);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return Task.CompletedTask;
        }

        public async Task<string> EditFileAsync(IFormFile file, string route, string containerName)
        {
            await DeleteFileAsync(route);
            return await SaveFileAsync(file, containerName);
        }
    }
}