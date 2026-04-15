using Employeemanagementpractice.Models;
using Employeemanagementpractice.Models.ViewModels;
using Employeemanagementpractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Employeemanagementpractice.Controllers
{
    [Authorize]
    public class StudentController : Controller
    {
        private readonly IStudentService _studentService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly Data.ApplicationDbContext _context;

        public StudentController(IStudentService studentService, UserManager<ApplicationUser> userManager, Data.ApplicationDbContext context)
        {
            _studentService = studentService;
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index(string? search, int? teamLeaderId, int page = 1, int pageSize = 15)
        {
            // Team Leaders only see their own students
            if (User.IsInRole("TeamLeader"))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var tl = await _context.TeamLeaders.FirstOrDefaultAsync(t => t.UserId == currentUser!.Id && t.IsActive);
                if (tl != null) teamLeaderId = tl.Id;
            }

            var (items, totalCount, totalPages) = await _studentService.SearchStudentsAsync(search, teamLeaderId, page, pageSize);

            ViewBag.TeamLeaders = await _studentService.GetTeamLeaderSelectListAsync();
            ViewBag.Search = search;
            ViewBag.TeamLeaderId = teamLeaderId;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.IsTeamLeader = User.IsInRole("TeamLeader");
            return View(items);
        }

        public async Task<IActionResult> Profile(int id)
        {
            var student = await _studentService.GetStudentProfileAsync(id);
            if (student == null) return NotFound();

            // Team leaders can only view their own students
            if (User.IsInRole("TeamLeader"))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var tl = await _context.TeamLeaders.FirstOrDefaultAsync(t => t.UserId == currentUser!.Id);
                if (tl == null || student.TeamLeaderId != tl.Id)
                    return Forbid();
            }

            return View(student);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Manager,Staff,TeamLeader")]
        public async Task<IActionResult> Create(int? teamLeaderId)
        {
            var vm = new StudentViewModel();

            // Team leaders can only create under themselves
            if (User.IsInRole("TeamLeader"))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var tl = await _context.TeamLeaders.FirstOrDefaultAsync(t => t.UserId == currentUser!.Id && t.IsActive);
                if (tl != null) vm.TeamLeaderId = tl.Id;
            }
            else if (teamLeaderId.HasValue)
            {
                vm.TeamLeaderId = teamLeaderId.Value;
            }

            ViewBag.TeamLeaders = await _studentService.GetTeamLeaderSelectListAsync();
            ViewBag.IsTeamLeader = User.IsInRole("TeamLeader");
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager,Staff,TeamLeader")]
        public async Task<IActionResult> Create(StudentViewModel model)
        {
            // Enforce TL can only create under themselves
            if (User.IsInRole("TeamLeader"))
            {
                var currentUser2 = await _userManager.GetUserAsync(User);
                var tl = await _context.TeamLeaders.FirstOrDefaultAsync(t => t.UserId == currentUser2!.Id && t.IsActive);
                if (tl != null) model.TeamLeaderId = tl.Id;
            }

            if (!ModelState.IsValid)
            {
                ViewBag.TeamLeaders = await _studentService.GetTeamLeaderSelectListAsync();
                ViewBag.IsTeamLeader = User.IsInRole("TeamLeader");
                return View(model);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var result = await _studentService.CreateStudentAsync(model, currentUser!.Id, currentUser.FullName);

            if (!result.Success)
            {
                ModelState.AddModelError(result.ErrorField ?? "", result.ErrorMessage!);
                ViewBag.TeamLeaders = await _studentService.GetTeamLeaderSelectListAsync();
                ViewBag.IsTeamLeader = User.IsInRole("TeamLeader");
                return View(model);
            }

            TempData["Success"] = $"Student {model.FirstName} {model.LastName} registered successfully!";
            return RedirectToAction("Profile", new { id = result.Data });
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Manager,Staff,TeamLeader")]
        public async Task<IActionResult> Edit(int id)
        {
            // Team leaders can only edit their own students
            if (User.IsInRole("TeamLeader"))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var tl = await _context.TeamLeaders.FirstOrDefaultAsync(t => t.UserId == currentUser!.Id);
                var student = await _studentService.GetStudentByIdAsync(id);
                if (tl == null || student == null || student.TeamLeaderId != tl.Id)
                    return Forbid();
            }

            var vm = await _studentService.GetStudentForEditAsync(id);
            if (vm == null) return NotFound();

            ViewBag.TeamLeaders = await _studentService.GetTeamLeaderSelectListAsync();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager,Staff,TeamLeader")]
        public async Task<IActionResult> Edit(StudentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.TeamLeaders = await _studentService.GetTeamLeaderSelectListAsync();
                return View(model);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var result = await _studentService.UpdateStudentAsync(model, currentUser!.Id, currentUser.FullName);

            if (!result.Success)
            {
                ModelState.AddModelError("", result.ErrorMessage!);
                ViewBag.TeamLeaders = await _studentService.GetTeamLeaderSelectListAsync();
                return View(model);
            }

            TempData["Success"] = "Student updated successfully!";
            return RedirectToAction("Profile", new { id = model.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager,Staff")]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var result = await _studentService.DeleteStudentAsync(id, currentUser!.Id, currentUser.FullName);
            if (!result.Success) return NotFound();

            TempData["Success"] = "Student deactivated successfully!";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> GetProfileImage(int id)
        {
            var imageData = await _studentService.GetProfileImageAsync(id);
            if (imageData == null)
                return File(GetDefaultAvatar(), "image/svg+xml");
            return File(imageData, "image/jpeg");
        }

        [HttpGet]
        public async Task<IActionResult> ValidateId(string idNumber)
        {
            var result = await _studentService.ValidateIdNumberAsync(idNumber);
            return Json(result);
        }

        private static byte[] GetDefaultAvatar()
        {
            var svg = @"<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100' viewBox='0 0 100 100'>
                <circle cx='50' cy='50' r='50' fill='#6c757d'/><text x='50' y='55' text-anchor='middle' fill='white' font-size='40'>?</text></svg>";
            return System.Text.Encoding.UTF8.GetBytes(svg);
        }
    }
}
