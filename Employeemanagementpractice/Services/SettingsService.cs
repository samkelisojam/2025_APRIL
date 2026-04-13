using Employeemanagementpractice.Data;
using Employeemanagementpractice.Models;
using Employeemanagementpractice.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Employeemanagementpractice.Services
{
    public interface ISettingsService
    {
        Task<(List<UserSettingsViewModel> Users, int TotalCount, int TotalPages, List<string> AllRoles)> GetUsersAsync(string? search, int page, int pageSize);
        Task<List<string>> GetAllRolesAsync();
        Task<ServiceResult> CreateUserAsync(RegisterUserViewModel model, string currentUserId, string currentUserName);
        Task<ServiceResult> ToggleLockAsync(string userId, string currentUserId, string currentUserName);
        Task<ServiceResult> ChangeRoleAsync(string userId, string role, string currentUserId, string currentUserName);
        Task<AccessControlViewModel> GetAccessControlAsync();
        Task<ServiceResult> UpdatePermissionAsync(string roleName, int permissionId, bool canView, bool canCreate, bool canEdit, bool canDelete,
            string currentUserId, string currentUserName);
        Task<ServiceResult> CreateRoleAsync(string roleName, string currentUserId, string currentUserName);
    }

    public class SettingsService : ISettingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAuditService _audit;

        public SettingsService(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, IAuditService audit)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _audit = audit;
        }

        public async Task<(List<UserSettingsViewModel> Users, int TotalCount, int TotalPages, List<string> AllRoles)> GetUsersAsync(
            string? search, int page, int pageSize)
        {
            var query = _userManager.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(u => u.FirstName.ToLower().Contains(s) || u.LastName.ToLower().Contains(s) || u.Email!.ToLower().Contains(s));
            }

            var count = await query.CountAsync();
            var users = await query.OrderBy(u => u.LastName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var userSettings = new List<UserSettingsViewModel>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                userSettings.Add(new UserSettingsViewModel
                {
                    UserId = u.Id,
                    Email = u.Email!,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    IsActive = u.IsActive,
                    IsLocked = u.IsLocked,
                    Roles = roles.ToList()
                });
            }

            var allRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
            var totalPages = (int)Math.Ceiling(count / (double)pageSize);

            return (userSettings, count, totalPages, allRoles);
        }

        public async Task<List<string>> GetAllRolesAsync()
        {
            return await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
        }

        public async Task<ServiceResult> CreateUserAsync(RegisterUserViewModel model, string currentUserId, string currentUserName)
        {
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

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                var sr = ServiceResult.Fail("User creation failed.");
                foreach (var e in result.Errors)
                    sr.ValidationErrors[e.Code] = e.Description;
                return sr;
            }

            if (!string.IsNullOrWhiteSpace(model.Role))
                await _userManager.AddToRoleAsync(user, model.Role);

            await _audit.LogAsync(currentUserId, currentUserName, "Create", "User", user.Id,
                description: $"Created user: {model.FirstName} {model.LastName} with role {model.Role}");

            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> ToggleLockAsync(string userId, string currentUserId, string currentUserName)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return ServiceResult.Fail("User not found.");

            user.IsLocked = !user.IsLocked;
            user.IsActive = !user.IsLocked;
            if (user.IsLocked)
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            else
                await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.UpdateAsync(user);

            await _audit.LogAsync(currentUserId, currentUserName, user.IsLocked ? "LockAccount" : "UnlockAccount",
                "User", userId, description: $"{(user.IsLocked ? "Locked" : "Unlocked")} user: {user.FullName}");

            // Return the lock state in ErrorMessage for display
            var sr = ServiceResult.Ok();
            sr.ErrorMessage = user.IsLocked ? "locked" : "unlocked";
            sr.ErrorField = user.FullName;
            return sr;
        }

        public async Task<ServiceResult> ChangeRoleAsync(string userId, string role, string currentUserId, string currentUserName)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return ServiceResult.Fail("User not found.");

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!string.IsNullOrWhiteSpace(role))
                await _userManager.AddToRoleAsync(user, role);

            await _audit.LogAsync(currentUserId, currentUserName, "ChangeRole", "User", userId,
                description: $"Changed role for {user.FullName} to {role}");

            return ServiceResult.Ok();
        }

        public async Task<AccessControlViewModel> GetAccessControlAsync()
        {
            var roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
            var permissions = await _context.Permissions.OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync();
            var rolePermissions = await _context.RolePermissions.Include(rp => rp.Permission).ToListAsync();

            return new AccessControlViewModel
            {
                AllRoles = roles,
                AllPermissions = permissions,
                RolePermissions = roles.Select(r => new RolePermissionGroup
                {
                    RoleName = r,
                    Permissions = rolePermissions.Where(rp => rp.RoleName == r).ToList()
                }).ToList()
            };
        }

        public async Task<ServiceResult> UpdatePermissionAsync(string roleName, int permissionId, bool canView, bool canCreate,
            bool canEdit, bool canDelete, string currentUserId, string currentUserName)
        {
            var existing = await _context.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleName == roleName && rp.PermissionId == permissionId);

            if (existing != null)
            {
                existing.CanView = canView;
                existing.CanCreate = canCreate;
                existing.CanEdit = canEdit;
                existing.CanDelete = canDelete;
            }
            else
            {
                _context.RolePermissions.Add(new RolePermission
                {
                    RoleName = roleName,
                    PermissionId = permissionId,
                    CanView = canView,
                    CanCreate = canCreate,
                    CanEdit = canEdit,
                    CanDelete = canDelete
                });
            }
            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, currentUserName, "UpdatePermission", "RolePermission",
                $"{roleName}_{permissionId}", description: $"Updated permission for role {roleName}");

            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> CreateRoleAsync(string roleName, string currentUserId, string currentUserName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                return ServiceResult.Fail("Role name is required");

            if (await _roleManager.RoleExistsAsync(roleName))
                return ServiceResult.Fail("Role already exists");

            await _roleManager.CreateAsync(new IdentityRole(roleName));

            await _audit.LogAsync(currentUserId, currentUserName, "Create", "Role", roleName,
                description: $"Created role: {roleName}");

            return ServiceResult.Ok();
        }
    }
}
