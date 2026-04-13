using Employeemanagementpractice.Models;
using Employeemanagementpractice.Models.ViewModels;
using Employeemanagementpractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Employeemanagementpractice.Controllers
{
    [Authorize]
    public class TaskController : Controller
    {
        private readonly ITaskService _taskService;
        private readonly UserManager<ApplicationUser> _userManager;

        public TaskController(ITaskService taskService, UserManager<ApplicationUser> userManager)
        {
            _taskService = taskService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? status, string? priority, int page = 1)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = User.IsInRole("Admin") || User.IsInRole("Manager");

            var (items, totalCount, totalPages) = await _taskService.SearchAsync(status, priority, currentUser!.Id, isAdmin, page, 10);

            ViewBag.Status = status;
            ViewBag.Priority = priority;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            return View(items);
        }

        public async Task<IActionResult> Details(int id)
        {
            var task = await _taskService.GetDetailsAsync(id);
            if (task == null) return NotFound();

            ViewBag.Users = await _taskService.GetUserSelectListAsync();
            return View(task);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Users = await _taskService.GetUserSelectListAsync();
            return View(new TaskViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TaskViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Users = await _taskService.GetUserSelectListAsync();
                return View(model);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var result = await _taskService.CreateAsync(model, currentUser!.Id, currentUser.FullName);

            TempData["Success"] = "Task created successfully!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, Models.TaskStatus status)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var result = await _taskService.UpdateStatusAsync(id, status, currentUser!.Id, currentUser.FullName);
            if (!result.Success) return NotFound();

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int taskId, string comment, IFormFile? attachment)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            await _taskService.AddCommentAsync(taskId, comment, attachment, currentUser!.Id, currentUser.FullName);

            TempData["Success"] = "Comment added!";
            return RedirectToAction("Details", new { id = taskId });
        }

        public async Task<IActionResult> DownloadAttachment(int id)
        {
            var (data, contentType, fileName) = await _taskService.GetAttachmentAsync(id);
            if (data == null) return NotFound();
            return File(data, contentType!, fileName!);
        }
    }
}
