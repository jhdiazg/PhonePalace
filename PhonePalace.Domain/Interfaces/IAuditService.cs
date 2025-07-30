using System.Threading.Tasks;

namespace PhonePalace.Domain.Interfaces
{
    public interface IAuditService
    {
        Task LogAsync(string origin, string description);
    }
}