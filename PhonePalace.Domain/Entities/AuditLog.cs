using System;

namespace PhonePalace.Domain.Entities
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? SessionId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Origin { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? IPAddress { get; set; }
    }
}