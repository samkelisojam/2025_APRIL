using Employeemanagementpractice.Data;
using Employeemanagementpractice.Models;
using Microsoft.EntityFrameworkCore;

namespace Employeemanagementpractice.Services
{
    public interface IAuditLogService
    {
        Task<(List<AuditLog> Items, int TotalCount, int TotalPages, List<string> Actions, List<string> EntityTypes)> SearchAsync(
            string? search, string? action, string? entityType, DateTime? from, DateTime? to, int page, int pageSize);
        Task<AuditLog?> GetDetailsAsync(long id);
    }

    public class AuditLogService : IAuditLogService
    {
        private readonly ApplicationDbContext _context;

        public AuditLogService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(List<AuditLog> Items, int TotalCount, int TotalPages, List<string> Actions, List<string> EntityTypes)> SearchAsync(
            string? search, string? action, string? entityType, DateTime? from, DateTime? to, int page, int pageSize)
        {
            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(a => (a.UserName != null && a.UserName.ToLower().Contains(s)) ||
                    (a.Description != null && a.Description.ToLower().Contains(s)) ||
                    (a.EntityId != null && a.EntityId.Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(a => a.Action == action);

            if (!string.IsNullOrWhiteSpace(entityType))
                query = query.Where(a => a.EntityType == entityType);

            if (from.HasValue)
                query = query.Where(a => a.Timestamp >= from.Value);

            if (to.HasValue)
                query = query.Where(a => a.Timestamp <= to.Value.AddDays(1));

            var count = await query.CountAsync();
            var items = await query.OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var totalPages = (int)Math.Ceiling(count / (double)pageSize);

            var actions = await _context.AuditLogs.Select(a => a.Action).Distinct().ToListAsync();
            var entityTypes = await _context.AuditLogs.Where(a => a.EntityType != null).Select(a => a.EntityType!).Distinct().ToListAsync();

            return (items, count, totalPages, actions, entityTypes);
        }

        public async Task<AuditLog?> GetDetailsAsync(long id)
        {
            return await _context.AuditLogs.Include(a => a.User).FirstOrDefaultAsync(a => a.Id == id);
        }
    }
}
