using Employeemanagementpractice.Data;
using Employeemanagementpractice.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Employeemanagementpractice.Services
{
    public interface IUserActivityService
    {
        Task LogActivityAsync(string userId, string userName, string fullName, string role,
            string activityType, string? description = null, string? pageUrl = null,
            string? controller = null, string? actionName = null);
        Task<(List<UserActivity> Items, int TotalCount, int TotalPages)> SearchAsync(
            string? search, string? activityType, string? userId, DateTime? from, DateTime? to, int page, int pageSize);
        Task<object> GetStatsAsync(DateTime? from, DateTime? to);
        Task<List<object>> GetTimelineAsync(string? userId, int count = 50);
    }

    public class UserActivityService : IUserActivityService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserActivityService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogActivityAsync(string userId, string userName, string fullName, string role,
            string activityType, string? description = null, string? pageUrl = null,
            string? controller = null, string? actionName = null)
        {
            var http = _httpContextAccessor.HttpContext;
            var ua = http?.Request?.Headers["User-Agent"].ToString() ?? "";
            var browser = ParseBrowser(ua);

            var activity = new UserActivity
            {
                UserId = userId,
                UserName = userName,
                FullName = fullName,
                Role = role,
                ActivityType = activityType,
                Description = description,
                PageUrl = pageUrl,
                Controller = controller,
                ActionName = actionName,
                IpAddress = http?.Connection?.RemoteIpAddress?.ToString(),
                Browser = browser,
                Timestamp = DateTime.UtcNow
            };

            _context.UserActivities.Add(activity);
            await _context.SaveChangesAsync();
        }

        public async Task<(List<UserActivity> Items, int TotalCount, int TotalPages)> SearchAsync(
            string? search, string? activityType, string? userId, DateTime? from, DateTime? to, int page, int pageSize)
        {
            var query = _context.UserActivities.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(a => (a.FullName != null && a.FullName.ToLower().Contains(s)) ||
                    (a.UserName != null && a.UserName.ToLower().Contains(s)) ||
                    (a.Description != null && a.Description.ToLower().Contains(s)) ||
                    (a.PageUrl != null && a.PageUrl.ToLower().Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(activityType))
                query = query.Where(a => a.ActivityType == activityType);

            if (!string.IsNullOrWhiteSpace(userId))
                query = query.Where(a => a.UserId == userId);

            if (from.HasValue)
                query = query.Where(a => a.Timestamp >= from.Value);

            if (to.HasValue)
                query = query.Where(a => a.Timestamp <= to.Value.AddDays(1));

            var count = await query.CountAsync();
            var items = await query.OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var totalPages = (int)Math.Ceiling(count / (double)pageSize);

            return (items, count, totalPages);
        }

        public async Task<object> GetStatsAsync(DateTime? from, DateTime? to)
        {
            var query = _context.UserActivities.AsQueryable();
            if (from.HasValue) query = query.Where(a => a.Timestamp >= from.Value);
            if (to.HasValue) query = query.Where(a => a.Timestamp <= to.Value.AddDays(1));

            var total = await query.CountAsync();
            var logins = await query.CountAsync(a => a.ActivityType == "Login");
            var pageViews = await query.CountAsync(a => a.ActivityType == "PageView");
            var downloads = await query.CountAsync(a => a.ActivityType == "Download");
            var creates = await query.CountAsync(a => a.ActivityType == "Create");
            var edits = await query.CountAsync(a => a.ActivityType == "Edit");
            var exports = await query.CountAsync(a => a.ActivityType == "Export");
            var uniqueUsers = await query.Select(a => a.UserId).Distinct().CountAsync();

            var byUser = await query.GroupBy(a => new { a.UserId, a.FullName, a.Role })
                .Select(g => new { g.Key.UserId, g.Key.FullName, g.Key.Role, Count = g.Count(), LastActive = g.Max(a => a.Timestamp) })
                .OrderByDescending(x => x.Count).Take(10).ToListAsync();

            var byHour = await query.GroupBy(a => a.Timestamp.Hour)
                .Select(g => new { Hour = g.Key, Count = g.Count() })
                .OrderBy(x => x.Hour).ToListAsync();

            var byType = await query.GroupBy(a => a.ActivityType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).ToListAsync();

            return new
            {
                total, logins, pageViews, downloads, creates, edits, exports, uniqueUsers,
                topUsers = byUser.Select(u => new { u.FullName, u.Role, u.Count, u.LastActive }),
                byHour = byHour.Select(h => new { h.Hour, h.Count }),
                byType = byType.Select(t => new { t.Type, t.Count })
            };
        }

        public async Task<List<object>> GetTimelineAsync(string? userId, int count = 50)
        {
            var query = _context.UserActivities.AsQueryable();
            if (!string.IsNullOrWhiteSpace(userId))
                query = query.Where(a => a.UserId == userId);

            var items = await query.OrderByDescending(a => a.Timestamp)
                .Take(count)
                .Select(a => new
                {
                    a.FullName,
                    a.Role,
                    a.ActivityType,
                    a.Description,
                    a.PageUrl,
                    a.Browser,
                    a.IpAddress,
                    a.Timestamp
                }).ToListAsync();

            return items.Cast<object>().ToList();
        }

        private static string ParseBrowser(string ua)
        {
            if (string.IsNullOrEmpty(ua)) return "Unknown";
            if (ua.Contains("Edg/")) return "Edge";
            if (ua.Contains("Chrome/")) return "Chrome";
            if (ua.Contains("Firefox/")) return "Firefox";
            if (ua.Contains("Safari/")) return "Safari";
            if (ua.Contains("Opera") || ua.Contains("OPR/")) return "Opera";
            return "Other";
        }
    }

    // Middleware to auto-track page views
    public class ActivityTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly HashSet<string> _trackedExtensions = new() { "", ".html" };
        private static readonly HashSet<string> _ignoredPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            "/notificationHub", "/Search/Query", "/Home/CheckUpdates",
            "/favicon.ico", "/_framework", "/__browser"
        };

        public ActivityTrackingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);

            // Only track authenticated GET requests to controllers (not static files, APIs, etc.)
            if (context.User?.Identity?.IsAuthenticated == true &&
                context.Request.Method == "GET" &&
                context.Response.StatusCode == 200)
            {
                var path = context.Request.Path.Value ?? "";
                var ext = Path.GetExtension(path);

                // Skip static files, SignalR, search endpoints
                if (!_trackedExtensions.Contains(ext)) return;
                if (_ignoredPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))) return;
                if (path.StartsWith("/css") || path.StartsWith("/js") || path.StartsWith("/lib") || path.StartsWith("/images")) return;

                try
                {
                    var routeData = context.GetRouteData();
                    var controller = routeData?.Values["controller"]?.ToString();
                    var action = routeData?.Values["action"]?.ToString();

                    if (string.IsNullOrEmpty(controller)) return;

                    using var scope = context.RequestServices.CreateScope();
                    var activityService = scope.ServiceProvider.GetRequiredService<IUserActivityService>();
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                    var user = await userManager.GetUserAsync(context.User);
                    if (user == null) return;

                    var roles = await userManager.GetRolesAsync(user);
                    var role = roles.FirstOrDefault() ?? "Unknown";

                    var description = $"Viewed {controller}/{action}";
                    if (action == "Download" || path.Contains("Download") || path.Contains("Export"))
                        await activityService.LogActivityAsync(user.Id, user.Email ?? "", user.FullName, role, "Download", $"Downloaded from {controller}/{action}", path, controller, action);
                    else
                        await activityService.LogActivityAsync(user.Id, user.Email ?? "", user.FullName, role, "PageView", description, path, controller, action);
                }
                catch
                {
                    // Silent fail - don't break the app for activity tracking
                }
            }
        }
    }
}
