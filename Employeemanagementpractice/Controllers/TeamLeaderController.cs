using Employeemanagementpractice.Models;
using Employeemanagementpractice.Models.ViewModels;
using Employeemanagementpractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Employeemanagementpractice.Controllers
{
    [Authorize(Roles = "Admin,Manager,Staff,ReadOnly")]
    public class TeamLeaderController : Controller
    {
        private readonly ITeamLeaderService _teamLeaderService;
        private readonly UserManager<ApplicationUser> _userManager;

        public TeamLeaderController(ITeamLeaderService teamLeaderService, UserManager<ApplicationUser> userManager)
        {
            _teamLeaderService = teamLeaderService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? search, int page = 1, int pageSize = 10)
        {
            var (items, totalCount, totalPages) = await _teamLeaderService.SearchAsync(search, page, pageSize);

            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            return View(items);
        }

        public async Task<IActionResult> Details(int id)
        {
            var tl = await _teamLeaderService.GetDetailsAsync(id);
            if (tl == null) return NotFound();
            return View(tl);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        public IActionResult Create() => View(new TeamLeaderViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Create(TeamLeaderViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var currentUser = await _userManager.GetUserAsync(User);
            var result = await _teamLeaderService.CreateAsync(model, currentUser!.Id, currentUser.FullName);

            if (!result.Success)
            {
                if (result.ErrorField != null)
                    ModelState.AddModelError(result.ErrorField, result.ErrorMessage!);
                foreach (var err in result.ValidationErrors)
                    ModelState.AddModelError("", err.Value);
                return View(model);
            }

            TempData["Success"] = $"Team Leader {model.FirstName} {model.LastName} created successfully! Temp password: {result.ErrorMessage}";
            return RedirectToAction("Index");
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _teamLeaderService.GetForEditAsync(id);
            if (vm == null) return NotFound();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Edit(TeamLeaderViewModel model)
        {
            ModelState.Remove("Password");
            if (!ModelState.IsValid) return View(model);

            var currentUser = await _userManager.GetUserAsync(User);
            var result = await _teamLeaderService.UpdateAsync(model, currentUser!.Id, currentUser.FullName);

            if (!result.Success) return NotFound();

            TempData["Success"] = "Team Leader updated successfully!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var result = await _teamLeaderService.DeleteAsync(id, currentUser!.Id, currentUser.FullName);
            if (!result.Success) return NotFound();

            TempData["Success"] = "Team Leader deactivated successfully!";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Students(int id, string? search, int page = 1)
        {
            var data = await _teamLeaderService.GetStudentsAsync(id, search, page, 10);
            if (data == null) return NotFound();

            ViewBag.TeamLeader = data.Value.TeamLeader;
            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.TotalPages = data.Value.TotalPages;
            ViewBag.TotalCount = data.Value.TotalCount;
            return View(data.Value.Students);
        }
    }
}
