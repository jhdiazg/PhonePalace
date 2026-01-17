using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace PhonePalace.Domain.Interfaces
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(IFormFile file, string containerName);
        Task DeleteFileAsync(string route);
        Task<string> EditFileAsync(IFormFile file, string route, string containerName);
    }
}