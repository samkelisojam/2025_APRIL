using Employeemanagementpractice.Models;
using Employeemanagementpractice.Models.ViewModels;
using Employeemanagementpractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Employeemanagementpractice.Data;

namespace Employeemanagementpractice.Controllers
{
    [Authorize(Roles = "Admin,Manager,Staff,ReadOnly,TeamLeader")]
    public class TeamLeaderController : Controller
    {
        private readonly ITeamLeaderService _teamLeaderService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public TeamLeaderController(ITeamLeaderService teamLeaderService, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _teamLeaderService = teamLeaderService;
            _userManager = userManager;
            _context = context;
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

        // ── TeamLeader Dashboard: Horizontal Tabs (Attendance, Timesheet, Tasks) ──
        [HttpGet]
        public async Task<IActionResult> Dashboard(int? id)
        {
            TeamLeader? tl = null;
            if (id.HasValue)
            {
                tl = await _context.TeamLeaders.Include(t => t.User)
                    .Include(t => t.Students.Where(s => s.IsActive))
                    .FirstOrDefaultAsync(t => t.Id == id.Value);
            }
            else if (User.IsInRole("TeamLeader"))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                    tl = await _context.TeamLeaders.Include(t => t.User)
                        .Include(t => t.Students.Where(s => s.IsActive))
                        .FirstOrDefaultAsync(t => t.UserId == currentUser.Id && t.IsActive);
            }
            if (tl == null) return RedirectToAction("Index");

            // All team leaders for dropdown (admin/manager)
            ViewBag.TeamLeaders = await _context.TeamLeaders.Include(t => t.User).Where(t => t.IsActive).ToListAsync();
            ViewBag.TeamLeader = tl;
            return View(tl);
        }

        // ── API: Get attendance records for a day (queried by team leader's students) ──
        [HttpGet]
        public async Task<IActionResult> DayRecords(int teamLeaderId, DateTime date)
        {
            var records = await _context.AttendanceRecords
                .Include(a => a.Student)
                .Where(a => a.Student.TeamLeaderId == teamLeaderId && a.Date.Date == date.Date && a.Student.IsActive)
                .OrderBy(a => a.Student.LastName)
                .Select(a => new {
                    studentName = a.Student.FirstName + " " + a.Student.LastName,
                    studentId = a.StudentId,
                    status = a.Status,
                    clockIn = a.ClockInTime.HasValue ? a.ClockInTime.Value.ToString("HH:mm") : "-",
                    clockOut = a.ClockOutTime.HasValue ? a.ClockOutTime.Value.ToString("HH:mm") : "-",
                    hours = a.HoursWorked.HasValue ? a.HoursWorked.Value.ToString("F1") : "-",
                    address = a.ClockInAddress ?? "-",
                    device = a.ClockInDeviceName ?? "-"
                })
                .ToListAsync();

            // Also get students with NO record for that day
            var recordedIds = records.Select(r => r.studentId).ToList();
            var absent = await _context.Students
                .Where(s => s.TeamLeaderId == teamLeaderId && s.IsActive && !recordedIds.Contains(s.Id))
                .Select(s => new {
                    studentName = s.FirstName + " " + s.LastName,
                    studentId = s.Id,
                    status = "Absent",
                    clockIn = "-",
                    clockOut = "-",
                    hours = "-",
                    address = "-",
                    device = "-"
                })
                .ToListAsync();

            return Json(records.Concat(absent));
        }

        // ── API: Monthly Timesheet for a team leader's students ──
        [HttpGet]
        public async Task<IActionResult> MonthlyTimesheet(int teamLeaderId, int year, int month)
        {
            var firstDay = new DateTime(year, month, 1);
            var lastDay = firstDay.AddMonths(1).AddDays(-1);
            var daysInMonth = DateTime.DaysInMonth(year, month);

            var students = await _context.Students
                .Where(s => s.TeamLeaderId == teamLeaderId && s.IsActive)
                .OrderBy(s => s.LastName)
                .Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName })
                .ToListAsync();

            var records = await _context.AttendanceRecords
                .Where(a => a.Student.TeamLeaderId == teamLeaderId && a.Date >= firstDay && a.Date <= lastDay && a.Student.IsActive)
                .ToListAsync();

            var result = students.Select(s => {
                var studentRecords = records.Where(r => r.StudentId == s.Id).ToList();
                var days = new object[daysInMonth];
                for (int d = 0; d < daysInMonth; d++)
                {
                    var rec = studentRecords.FirstOrDefault(r => r.Date.Day == d + 1);
                    days[d] = rec != null ? new {
                        status = rec.Status,
                        clockIn = rec.ClockInTime?.ToString("HH:mm"),
                        clockOut = rec.ClockOutTime?.ToString("HH:mm"),
                        hours = rec.HoursWorked?.ToString("F1")
                    } : (object)new { status = (string?)null, clockIn = (string?)null, clockOut = (string?)null, hours = (string?)null };
                }
                var totalHours = studentRecords.Where(r => r.HoursWorked.HasValue).Sum(r => r.HoursWorked!.Value);
                var presentDays = studentRecords.Count(r => r.Status == "Present" || r.Status == "Late");
                var absentDays = studentRecords.Count(r => r.Status == "Absent");
                return new {
                    studentId = s.Id, name = s.Name, days,
                    totalHours = Math.Round(totalHours, 1), presentDays, absentDays
                };
            });

            return Json(new { year, month, daysInMonth, students = result });
        }

        // ── API: Tasks by student for a team leader ──
        [HttpGet]
        public async Task<IActionResult> TasksByStudent(int teamLeaderId)
        {
            var students = await _context.Students
                .Where(s => s.TeamLeaderId == teamLeaderId && s.IsActive)
                .Select(s => new {
                    s.Id,
                    Name = s.FirstName + " " + s.LastName,
                    Tasks = _context.TaskItems
                        .Where(t => t.AssignedToUserId == s.UserId)
                        .Select(t => new {
                            t.Id, t.Title, Status = t.Status.ToString(),
                            Priority = t.Priority.ToString(),
                            DueDate = t.DueDate,
                            IsOverdue = t.DueDate.HasValue && t.DueDate.Value < SastClock.Now && t.Status != Models.TaskStatus.Completed
                        }).ToList()
                })
                .ToListAsync();

            // Format dates outside of EF query
            var result = students.Select(s => new {
                s.Id, s.Name,
                Tasks = s.Tasks.Select(t => new {
                    t.Id, t.Title, t.Status, t.Priority,
                    DueDate = t.DueDate.HasValue ? t.DueDate.Value.ToString("yyyy-MM-dd") : "-",
                    t.IsOverdue
                })
            });

            return Json(result);
            return Json(students);
        }
    }
}
