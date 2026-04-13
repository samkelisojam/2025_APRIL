using Employeemanagementpractice.Models;
using Employeemanagementpractice.Models.ViewModels;
using Employeemanagementpractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Employeemanagementpractice.Controllers
{
    [Authorize]
    public class AnnouncementController : Controller
    {
        private readonly IAnnouncementService _announcementService;
        private readonly UserManager<ApplicationUser> _userManager;

        public AnnouncementController(IAnnouncementService announcementService, UserManager<ApplicationUser> userManager)
        {
            _announcementService = announcementService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            var (items, _, totalPages) = await _announcementService.SearchAsync(page, 10);

            // Pass role info for create button visibility
            ViewBag.CanCreate = User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("Staff");
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            return View(items);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Manager,Staff")]
        public IActionResult Create() => View(new AnnouncementViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager,Staff")]
        public async Task<IActionResult> Create(AnnouncementViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var currentUser = await _userManager.GetUserAsync(User);
            await _announcementService.CreateAsync(model, currentUser!.Id, currentUser.FullName);

            TempData["Success"] = "Announcement published!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _announcementService.DeleteAsync(id);
            if (!result.Success) return NotFound();

            TempData["Success"] = "Announcement removed!";
            return RedirectToAction("Index");
        }
    }
}
