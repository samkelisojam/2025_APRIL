using Employeemanagementpractice.Data;
using Employeemanagementpractice.Hubs;
using Employeemanagementpractice.Models;
using Employeemanagementpractice.Models.ViewModels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Employeemanagementpractice.Services
{
    public interface IAnnouncementService
    {
        Task<(List<Announcement> Items, int TotalCount, int TotalPages)> SearchAsync(int page, int pageSize);
        Task<ServiceResult<int>> CreateAsync(AnnouncementViewModel model, string currentUserId, string currentUserName);
        Task<ServiceResult> DeleteAsync(int id);
    }

    public class AnnouncementService : IAnnouncementService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;
        private readonly IHubContext<NotificationHub> _hubContext;

        public AnnouncementService(ApplicationDbContext context, IAuditService audit, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _audit = audit;
            _hubContext = hubContext;
        }

        public async Task<(List<Announcement> Items, int TotalCount, int TotalPages)> SearchAsync(int page, int pageSize)
        {
            var query = _context.Announcements.Include(a => a.CreatedBy)
                .Where(a => a.IsActive)
                .OrderByDescending(a => a.CreatedAt);

            var count = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var totalPages = (int)Math.Ceiling(count / (double)pageSize);

            return (items, count, totalPages);
        }

        public async Task<ServiceResult<int>> CreateAsync(AnnouncementViewModel model, string currentUserId, string currentUserName)
        {
            var announcement = new Announcement
            {
                Title = model.Title,
                Message = model.Message,
                CreatedByUserId = currentUserId,
                TargetAudience = model.TargetAudience,
                ExpiresAt = model.ExpiresAt
            };

            _context.Announcements.Add(announcement);
            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("NewAnnouncement", model.Title, model.Message);

            await _audit.LogAsync(currentUserId, currentUserName, "Create", "Announcement",
                announcement.Id.ToString(), description: $"Created announcement: {model.Title}");

            return ServiceResult<int>.Ok(announcement.Id);
        }

        public async Task<ServiceResult> DeleteAsync(int id)
        {
            var ann = await _context.Announcements.FindAsync(id);
            if (ann == null)
                return ServiceResult.Fail("Announcement not found.");

            ann.IsActive = false;
            await _context.SaveChangesAsync();
            return ServiceResult.Ok();
        }
    }
}
