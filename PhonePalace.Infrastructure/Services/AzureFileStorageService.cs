// d:\PhonePalace\PhonePalace.Infrastructure\Services\AzureFileStorageService.cs
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PhonePalace.Domain.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PhonePalace.Infrastructure.Services
{
    public class AzureFileStorageService : IFileStorageService
    {
        private readonly string _connectionString;

        public AzureFileStorageService(IConfiguration configuration)
        {
            // Asegúrate de tener "AzureStorage" en tu appsettings.json o variables de entorno
            _connectionString = configuration.GetConnectionString("AzureStorage");
        }

        public async Task<string> SaveFileAsync(IFormFile file, string containerName)
        {
            var client = new BlobContainerClient(_connectionString, containerName);
            await client.CreateIfNotExistsAsync();
            await client.SetAccessPolicyAsync(PublicAccessType.Blob);

            var extension = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid()}{extension}";
            var blob = client.GetBlobClient(fileName);

            using (var stream = file.OpenReadStream())
            {
                await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });
            }

            return blob.Uri.ToString();
        }

        public async Task DeleteFileAsync(string route)
        {
            if (string.IsNullOrEmpty(route))
            {
                return;
            }

            try
            {
                var uri = new Uri(route);
                var containerName = uri.Segments[1].TrimEnd('/');
                var fileName = Path.GetFileName(uri.LocalPath);

                var client = new BlobContainerClient(_connectionString, containerName);
                var blob = client.GetBlobClient(fileName);
                await blob.DeleteIfExistsAsync();
            }
            catch
            {
                // Ignorar errores si la URI no es válida o no se puede borrar
            }
        }

        public async Task<string> EditFileAsync(IFormFile file, string route, string containerName)
        {
            await DeleteFileAsync(route);
            return await SaveFileAsync(file, containerName);
        }
    }
}
