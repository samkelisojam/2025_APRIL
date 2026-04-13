using Employeemanagementpractice.Data;
using Employeemanagementpractice.Models;
using Employeemanagementpractice.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Employeemanagementpractice.Services
{
    public interface ITeamLeaderService
    {
        Task<(List<TeamLeader> Items, int TotalCount, int TotalPages)> SearchAsync(string? search, int page, int pageSize);
        Task<TeamLeader?> GetDetailsAsync(int id);
        Task<ServiceResult<int>> CreateAsync(TeamLeaderViewModel model, string currentUserId, string currentUserName);
        Task<TeamLeaderViewModel?> GetForEditAsync(int id);
        Task<ServiceResult> UpdateAsync(TeamLeaderViewModel model, string currentUserId, string currentUserName);
        Task<ServiceResult> DeleteAsync(int id, string currentUserId, string currentUserName);
        Task<(List<Student> Students, TeamLeader TeamLeader, int TotalCount, int TotalPages)?> GetStudentsAsync(int id, string? search, int page, int pageSize);
    }

    public class TeamLeaderService : ITeamLeaderService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _audit;

        public TeamLeaderService(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IAuditService audit)
        {
            _context = context;
            _userManager = userManager;
            _audit = audit;
        }

        public async Task<(List<TeamLeader> Items, int TotalCount, int TotalPages)> SearchAsync(string? search, int page, int pageSize)
        {
            var query = _context.TeamLeaders.Include(t => t.User).Include(t => t.Students).Where(t => t.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(t => t.User.FirstName.ToLower().Contains(s) ||
                    t.User.LastName.ToLower().Contains(s) ||
                    t.EmployeeNumber.ToLower().Contains(s) ||
                    (t.Department != null && t.Department.ToLower().Contains(s)));
            }

            var count = await query.CountAsync();
            var items = await query.OrderBy(t => t.User.LastName)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var totalPages = (int)Math.Ceiling(count / (double)pageSize);

            return (items, count, totalPages);
        }

        public async Task<TeamLeader?> GetDetailsAsync(int id)
        {
            return await _context.TeamLeaders
                .Include(t => t.User).Include(t => t.Students)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<ServiceResult<int>> CreateAsync(TeamLeaderViewModel model, string currentUserId, string currentUserName)
        {
            if (await _context.TeamLeaders.AnyAsync(t => t.EmployeeNumber == model.EmployeeNumber))
                return ServiceResult<int>.Fail("Employee number already exists.", "EmployeeNumber");

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                PhoneNumber = model.PhoneNumber,
                EmailConfirmed = true,
                IsActive = true
            };

            var password = model.Password ?? $"TL@{DateTime.Now.Year}!";
            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var sr = ServiceResult<int>.Fail("User creation failed.");
                foreach (var e in result.Errors)
                    sr.ValidationErrors[e.Code] = e.Description;
                return sr;
            }

            await _userManager.AddToRoleAsync(user, "TeamLeader");

            var teamLeader = new TeamLeader
            {
                UserId = user.Id,
                EmployeeNumber = model.EmployeeNumber,
                Department = model.Department,
                MaxStudents = model.MaxStudents > 0 ? model.MaxStudents : 25
            };

            _context.TeamLeaders.Add(teamLeader);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, currentUserName, "Create", "TeamLeader",
                teamLeader.Id.ToString(), newValues: new { model.EmployeeNumber, model.FirstName, model.LastName },
                description: $"Created team leader: {model.FirstName} {model.LastName}");

            // Return the generated password in the error message field for display (not an error)
            var successResult = ServiceResult<int>.Ok(teamLeader.Id);
            successResult.ErrorMessage = password; // Reuse field for temp password
            return successResult;
        }

        public async Task<TeamLeaderViewModel?> GetForEditAsync(int id)
        {
            var tl = await _context.TeamLeaders.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == id);
            if (tl == null) return null;

            return new TeamLeaderViewModel
            {
                Id = tl.Id,
                UserId = tl.UserId,
                EmployeeNumber = tl.EmployeeNumber,
                Department = tl.Department,
                MaxStudents = tl.MaxStudents,
                FirstName = tl.User.FirstName,
                LastName = tl.User.LastName,
                Email = tl.User.Email!,
                PhoneNumber = tl.User.PhoneNumber
            };
        }

        public async Task<ServiceResult> UpdateAsync(TeamLeaderViewModel model, string currentUserId, string currentUserName)
        {
            var tl = await _context.TeamLeaders.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == model.Id);
            if (tl == null)
                return ServiceResult.Fail("Team leader not found.");

            tl.EmployeeNumber = model.EmployeeNumber;
            tl.Department = model.Department;
            tl.MaxStudents = model.MaxStudents;
            tl.User.FirstName = model.FirstName;
            tl.User.LastName = model.LastName;
            tl.User.PhoneNumber = model.PhoneNumber;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, currentUserName, "Update", "TeamLeader",
                tl.Id.ToString(), description: $"Updated team leader: {model.FirstName} {model.LastName}");

            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> DeleteAsync(int id, string currentUserId, string currentUserName)
        {
            var tl = await _context.TeamLeaders.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == id);
            if (tl == null)
                return ServiceResult.Fail("Team leader not found.");

            tl.IsActive = false;
            tl.User.IsActive = false;
            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, currentUserName, "Delete", "TeamLeader",
                id.ToString(), description: $"Deactivated team leader: {tl.User.FullName}");

            return ServiceResult.Ok();
        }

        public async Task<(List<Student> Students, TeamLeader TeamLeader, int TotalCount, int TotalPages)?> GetStudentsAsync(
            int id, string? search, int page, int pageSize)
        {
            var tl = await _context.TeamLeaders.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == id);
            if (tl == null) return null;

            var query = _context.Students.Where(s => s.TeamLeaderId == id && s.IsActive);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(st => st.FirstName.ToLower().Contains(s) || st.LastName.ToLower().Contains(s) || st.SAIDNumber.Contains(s));
            }

            var count = await query.CountAsync();
            var students = await query.OrderBy(s => s.LastName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var totalPages = (int)Math.Ceiling(count / (double)pageSize);

            return (students, tl, count, totalPages);
        }
    }
}
