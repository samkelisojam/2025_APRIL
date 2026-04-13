using Employeemanagementpractice.Models;
using Microsoft.AspNetCore.Identity;

namespace Employeemanagementpractice.Services
{
    public interface IAccountService
    {
        Task<ServiceResult> LoginAsync(string email, string password, bool rememberMe);
        Task LogoutAsync(string? currentUserId, string? currentUserName);
    }

    public class AccountService : IAccountService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IAuditService _audit;
        private readonly IUserActivityService _activity;

        public AccountService(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
            IAuditService audit, IUserActivityService activity)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _audit = audit;
            _activity = activity;
        }

        public async Task<ServiceResult> LoginAsync(string email, string password, bool rememberMe)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return ServiceResult.Fail("Invalid email or password.");

            if (!user.IsActive || user.IsLocked)
                return ServiceResult.Fail("Your account is locked or inactive. Contact administrator.");

            var result = await _signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
                await _audit.LogAsync(user.Id, user.FullName, "Login", "User", user.Id, description: "User logged in");
                var roles = await _userManager.GetRolesAsync(user);
                await _activity.LogActivityAsync(user.Id, user.Email ?? "", user.FullName, roles.FirstOrDefault() ?? "", "Login", $"{user.FullName} logged in");
                return ServiceResult.Ok();
            }

            if (result.IsLockedOut)
                return ServiceResult.Fail("Account is temporarily locked. Try again later.");

            return ServiceResult.Fail("Invalid email or password.");
        }

        public async Task LogoutAsync(string? currentUserId, string? currentUserName)
        {
            if (currentUserId != null && currentUserName != null)
            {
                await _audit.LogAsync(currentUserId, currentUserName, "Logout", "User", currentUserId, description: "User logged out");
                await _activity.LogActivityAsync(currentUserId, currentUserName, currentUserName, "", "Logout", $"{currentUserName} logged out");
            }

            await _signInManager.SignOutAsync();
        }
    }
}
