using Employeemanagementpractice.Models;
using Employeemanagementpractice.Models.ViewModels;
using Employeemanagementpractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Employeemanagementpractice.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class SettingsController : Controller
    {
        private readonly ISettingsService _settingsService;
        private readonly UserManager<ApplicationUser> _userManager;

        public SettingsController(ISettingsService settingsService, UserManager<ApplicationUser> userManager)
        {
            _settingsService = settingsService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Users(string? search, int page = 1)
        {
            var (users, totalCount, totalPages, allRoles) = await _settingsService.GetUsersAsync(search, page, 15);

            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.AllRoles = allRoles;
            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> CreateUser()
        {
            ViewBag.AllRoles = await _settingsService.GetAllRolesAsync();
            return View(new RegisterUserViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(RegisterUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.AllRoles = await _settingsService.GetAllRolesAsync();
                return View(model);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var result = await _settingsService.CreateUserAsync(model, currentUser!.Id, currentUser.FullName);

            if (!result.Success)
            {
                foreach (var err in result.ValidationErrors)
                    ModelState.AddModelError("", err.Value);
                ViewBag.AllRoles = await _settingsService.GetAllRolesAsync();
                return View(model);
            }

            TempData["Success"] = $"User {model.FirstName} {model.LastName} created!";
            return RedirectToAction("Users");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(string userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var result = await _settingsService.ToggleLockAsync(userId, currentUser!.Id, currentUser.FullName);
            if (!result.Success) return NotFound();

            TempData["Success"] = $"User {result.ErrorField} {result.ErrorMessage}!";
            return RedirectToAction("Users");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeRole(string userId, string role)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var result = await _settingsService.ChangeRoleAsync(userId, role, currentUser!.Id, currentUser.FullName);
            if (!result.Success) return NotFound();

            return Json(new { success = true });
        }

        public async Task<IActionResult> AccessControl()
        {
            var vm = await _settingsService.GetAccessControlAsync();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePermission(string roleName, int permissionId, bool canView, bool canCreate, bool canEdit, bool canDelete)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var result = await _settingsService.UpdatePermissionAsync(roleName, permissionId, canView, canCreate, canEdit, canDelete,
                currentUser!.Id, currentUser.FullName);

            return Json(new { success = result.Success });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(string roleName)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var result = await _settingsService.CreateRoleAsync(roleName, currentUser!.Id, currentUser.FullName);

            if (!result.Success)
                return Json(new { success = false, error = result.ErrorMessage });

            return Json(new { success = true });
        }
    }
}
