using System.Diagnostics;
using Employeemanagementpractice.Models;
using Employeemanagementpractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Employeemanagementpractice.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IDashboardService _dashboardService;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(IDashboardService dashboardService, UserManager<ApplicationUser> userManager)
        {
            _dashboardService = dashboardService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(currentUser!);
            var role = roles.Contains("Admin") ? "Admin" : roles.Contains("Manager") ? "Manager"
                : roles.Contains("Staff") ? "Staff" : roles.Contains("TeamLeader") ? "TeamLeader" : "ReadOnly";

            var vm = await _dashboardService.GetDashboardAsync(currentUser.Id, role);
            ViewBag.UserRole = role;
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> CheckUpdates(DateTime? since)
        {
            if (!since.HasValue) return Json(new { hasUpdates = false });
            var hasUpdates = await _dashboardService.HasUpdatesSinceAsync(since.Value);
            return Json(new { hasUpdates });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
