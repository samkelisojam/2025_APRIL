using Employeemanagementpractice.Models;
using Employeemanagementpractice.Models.ViewModels;
using Employeemanagementpractice.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Employeemanagementpractice.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAccountService _accountService;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(IAccountService accountService, UserManager<ApplicationUser> userManager)
        {
            _accountService = accountService;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View(model);

            var result = await _accountService.LoginAsync(model.Email, model.Password, model.RememberMe);

            if (result.Success)
                return LocalRedirect(returnUrl ?? "/");

            ModelState.AddModelError("", result.ErrorMessage!);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            string? userId = null, userName = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    userId = user.Id;
                    userName = user.FullName;
                }
            }

            await _accountService.LogoutAsync(userId, userName);
            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied() => View();

        [HttpGet]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> ChangePassword()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            ViewBag.MustChange = user.MustChangePassword;
            ViewBag.Deadline = user.PasswordChangeDeadline;
            ViewBag.UserName = user.FullName;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            ViewBag.MustChange = user.MustChangePassword;
            ViewBag.Deadline = user.PasswordChangeDeadline;
            ViewBag.UserName = user.FullName;

            if (!ModelState.IsValid) return View(model);

            if (model.NewPassword == model.CurrentPassword)
            {
                ModelState.AddModelError("NewPassword", "New password must be different from your current password.");
                return View(model);
            }

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
                return View(model);
            }

            // Clear the force-change flag
            user.MustChangePassword = false;
            user.PasswordChangeDeadline = null;
            await _userManager.UpdateAsync(user);

            TempData["Success"] = "Password changed successfully!";
            return RedirectToAction("Index", "Home");
        }
    }
}
