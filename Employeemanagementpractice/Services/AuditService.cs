using Employeemanagementpractice.Data;
using Employeemanagementpractice.Models;
using System.Text.Json;

namespace Employeemanagementpractice.Services
{
    public interface IAuditService
    {
        Task LogAsync(string userId, string userName, string action, string? entityType = null,
            string? entityId = null, object? oldValues = null, object? newValues = null, string? description = null);
    }

    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string userId, string userName, string action, string? entityType = null,
            string? entityId = null, object? oldValues = null, object? newValues = null, string? description = null)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var audit = new AuditLog
            {
                UserId = userId,
                UserName = userName,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
                NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
                IpAddress = httpContext?.Connection?.RemoteIpAddress?.ToString(),
                UserAgent = httpContext?.Request?.Headers["User-Agent"].ToString(),
                Description = description,
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync();
        }
    }
}
