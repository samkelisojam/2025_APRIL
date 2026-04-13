using Employeemanagementpractice.Data;
using Employeemanagementpractice.Models;
using Employeemanagementpractice.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Employeemanagementpractice.Services
{
    public interface IDashboardService
    {
        Task<DashboardViewModel> GetDashboardAsync(string? userId = null, string? role = null);
        Task<bool> HasUpdatesSinceAsync(DateTime since);
    }

    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;

        public DashboardService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardViewModel> GetDashboardAsync(string? userId = null, string? role = null)
        {
            var vm = new DashboardViewModel();

            // Team leader sees only their own data
            if (role == "TeamLeader" && userId != null)
            {
                var tl = await _context.TeamLeaders.FirstOrDefaultAsync(t => t.UserId == userId && t.IsActive);
                if (tl != null)
                {
                    vm.TotalTeamLeaders = 1;
                    vm.TotalStudents = await _context.Students.CountAsync(s => s.TeamLeaderId == tl.Id);
                    vm.ActiveStudents = await _context.Students.CountAsync(s => s.TeamLeaderId == tl.Id && s.IsActive);
                    vm.PendingTasks = await _context.TaskItems.CountAsync(t => (t.AssignedToUserId == userId || t.CreatedByUserId == userId) && (t.Status == Models.TaskStatus.New || t.Status == Models.TaskStatus.InProgress));
                    vm.TotalPayrollRecords = 0;
                    vm.TotalPayrollAmount = 0;
                    vm.RecentAnnouncements = await _context.Announcements.Include(a => a.CreatedBy)
                        .Where(a => a.IsActive && (a.TargetAudience == TargetAudience.All || a.TargetAudience == TargetAudience.TeamLeaders || a.CreatedByUserId == userId))
                        .OrderByDescending(a => a.CreatedAt).Take(5).ToListAsync();
                    vm.RecentTasks = await _context.TaskItems.Include(t => t.AssignedTo)
                        .Where(t => t.AssignedToUserId == userId || t.CreatedByUserId == userId)
                        .OrderByDescending(t => t.CreatedAt).Take(5).ToListAsync();
                    vm.TeamLeaderSummaries = await _context.TeamLeaders.Include(t => t.User).Include(t => t.Students)
                        .Where(t => t.Id == tl.Id)
                        .Select(t => new TeamLeaderSummary
                        {
                            TeamLeaderId = t.Id,
                            Name = t.User.FirstName + " " + t.User.LastName,
                            Department = t.Department ?? "",
                            StudentCount = t.Students.Count(s => s.IsActive),
                            MaxStudents = t.MaxStudents
                        }).ToListAsync();
                }
                return vm;
            }

            // Staff, Manager, Admin see everything
            vm.TotalTeamLeaders = await _context.TeamLeaders.CountAsync(t => t.IsActive);
            vm.TotalStudents = await _context.Students.CountAsync();
            vm.ActiveStudents = await _context.Students.CountAsync(s => s.IsActive);
            vm.PendingTasks = await _context.TaskItems.CountAsync(t => t.Status == Models.TaskStatus.New || t.Status == Models.TaskStatus.InProgress);

            // Only Admin/Manager see payroll
            if (role == "Admin" || role == "Manager")
            {
                vm.TotalPayrollRecords = await _context.PayrollRecords.CountAsync();
                vm.TotalPayrollAmount = await _context.PayrollRecords.SumAsync(p => (decimal?)p.Amount) ?? 0;
            }

            vm.RecentAnnouncements = await _context.Announcements.Include(a => a.CreatedBy)
                .Where(a => a.IsActive).OrderByDescending(a => a.CreatedAt).Take(5).ToListAsync();
            vm.RecentTasks = await _context.TaskItems.Include(t => t.AssignedTo)
                .OrderByDescending(t => t.CreatedAt).Take(5).ToListAsync();
            vm.TeamLeaderSummaries = await _context.TeamLeaders.Include(t => t.User).Include(t => t.Students)
                .Where(t => t.IsActive)
                .Select(t => new TeamLeaderSummary
                {
                    TeamLeaderId = t.Id,
                    Name = t.User.FirstName + " " + t.User.LastName,
                    Department = t.Department ?? "",
                    StudentCount = t.Students.Count(s => s.IsActive),
                    MaxStudents = t.MaxStudents
                }).ToListAsync();

            return vm;
        }

        public async Task<bool> HasUpdatesSinceAsync(DateTime since)
        {
            return await _context.AuditLogs.AnyAsync(a => a.Timestamp > since);
        }
    }
}
