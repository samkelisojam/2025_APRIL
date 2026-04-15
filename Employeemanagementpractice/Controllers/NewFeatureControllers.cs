using Employeemanagementpractice.Data;
using Employeemanagementpractice.Models;
using Employeemanagementpractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Employeemanagementpractice.Controllers
{
    // ═══════════════════════════════════════════
    // 1. TRAINING CONTROLLER
    // ═══════════════════════════════════════════
    [Authorize]
    public class TrainingController : Controller
    {
        private readonly ITrainingService _training;
        private readonly UserManager<ApplicationUser> _userManager;

        public TrainingController(ITrainingService training, UserManager<ApplicationUser> userManager)
        { _training = training; _userManager = userManager; }

        public async Task<IActionResult> Index(string? category)
        {
            var courses = await _training.GetCoursesAsync(category);
            var user = await _userManager.GetUserAsync(User);
            var progress = user != null ? await _training.GetUserProgressAsync(user.Id) : new List<TrainingProgress>();
            var stats = await _training.GetTrainingStatsAsync();
            ViewBag.Progress = progress;
            ViewBag.Stats = stats;
            ViewBag.Category = category;
            return View(courses);
        }

        public async Task<IActionResult> View(int id)
        {
            var course = await _training.GetCourseAsync(id);
            if (course == null) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
                await _training.UpdateProgressAsync(id, user.Id, 10); // Mark as started
            ViewBag.UserId = user?.Id;
            return View(course);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager,Staff,TeamLeader")]
        public async Task<IActionResult> UpdateProgress([FromBody] ProgressRequest req)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false });
            await _training.UpdateProgressAsync(req.CourseId, user.Id, req.Percent);
            return Json(new { success = true });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager,Staff,TeamLeader")]
        public async Task<IActionResult> Complete([FromBody] CompleteRequest req)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false });
            await _training.CompleteCourseAsync(req.CourseId, user.Id, req.QuizScore);
            return Json(new { success = true });
        }

        [Authorize(Roles = "Admin,Manager")]
        public IActionResult Create() => View(new TrainingCourse());

        [HttpPost, Authorize(Roles = "Admin,Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TrainingCourse course)
        {
            course.CreatedBy = User.Identity?.Name;
            await _training.CreateCourseAsync(course);
            TempData["Success"] = "Training course created!";
            return RedirectToAction("Index");
        }

        [HttpPost, Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Delete(int id)
        {
            await _training.DeleteCourseAsync(id);
            return Json(new { success = true });
        }
    }

    public class ProgressRequest { public int CourseId { get; set; } public int Percent { get; set; } }
    public class CompleteRequest { public int CourseId { get; set; } public int? QuizScore { get; set; } }

    // ═══════════════════════════════════════════
    // 2. BACKUP CONTROLLER
    // ═══════════════════════════════════════════
    [Authorize(Roles = "Admin")]
    public class BackupController : Controller
    {
        private readonly IBackupService _backup;

        public BackupController(IBackupService backup) => _backup = backup;

        public async Task<IActionResult> Index()
        {
            var history = await _backup.GetBackupHistoryAsync();
            return View(history);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string backupType = "Manual")
        {
            var result = await _backup.CreateBackupAsync(backupType, User.Identity?.Name ?? "System");
            if (result.Success) TempData["Success"] = $"Backup created: {result.Data?.FileName}";
            else TempData["Error"] = result.ErrorMessage;
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Download(int id)
        {
            var (data, fn) = await _backup.DownloadBackupAsync(id);
            if (data == null) return NotFound();
            return File(data, "application/octet-stream", fn!);
        }
    }

    // ═══════════════════════════════════════════
    // 4. GLOBAL SEARCH (API endpoint)
    // ═══════════════════════════════════════════
    [Authorize]
    public class SearchController : Controller
    {
        private readonly IGlobalSearchService _search;

        public SearchController(IGlobalSearchService search) => _search = search;

        [HttpGet]
        public async Task<IActionResult> Query(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Json(new { results = Array.Empty<object>(), total = 0 });
            var result = await _search.SearchAsync(q);
            return Json(result);
        }
    }

    // ═══════════════════════════════════════════
    // 5. CALENDAR CONTROLLER
    // ═══════════════════════════════════════════
    [Authorize]
    public class CalendarController : Controller
    {
        private readonly ICalendarService _calendar;
        private readonly UserManager<ApplicationUser> _userManager;

        public CalendarController(ICalendarService calendar, UserManager<ApplicationUser> userManager)
        { _calendar = calendar; _userManager = userManager; }

        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> GetEvents(DateTime start, DateTime end)
        {
            var events = await _calendar.GetEventsAsync(start, end);
            return Json(events.Select(e => new {
                id = e.Id, title = e.Title, start = e.StartDate.ToString("yyyy-MM-ddTHH:mm"),
                end = e.EndDate?.ToString("yyyy-MM-ddTHH:mm"), color = e.Color,
                allDay = e.IsAllDay, description = e.Description,
                extendedProps = new { e.Location, e.EventType }
            }));
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager,Staff,TeamLeader")]
        public async Task<IActionResult> Create([FromBody] CalendarEvent ev)
        {
            var user = await _userManager.GetUserAsync(User);
            ev.CreatedByUserId = user!.Id;
            var result = await _calendar.CreateEventAsync(ev);
            return Json(new { success = result.Success, id = result.Data });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager,Staff,TeamLeader")]
        public async Task<IActionResult> Delete([FromBody] IdRequest req)
        {
            var result = await _calendar.DeleteEventAsync(req.Id);
            return Json(new { success = result.Success });
        }

        [HttpGet]
        public async Task<IActionResult> Upcoming() =>
            Json(await _calendar.GetUpcomingDeadlinesAsync());
    }

    public class IdRequest { public int Id { get; set; } }

    // ═══════════════════════════════════════════
    // 6. BATCH IMPORT CONTROLLER
    // ═══════════════════════════════════════════
    [Authorize(Roles = "Admin,Manager")]
    public class ImportController : Controller
    {
        private readonly IBatchImportService _import;

        public ImportController(IBatchImportService import) => _import = import;

        public IActionResult Index() => View();

        [HttpGet]
        public IActionResult Template()
        {
            var data = _import.GetImportTemplate();
            return File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "StudentImportTemplate.xlsx");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select an Excel file";
                return RedirectToAction("Index");
            }
            using var stream = file.OpenReadStream();
            var result = await _import.ImportStudentsFromExcelAsync(stream, User.Identity?.Name ?? "");
            if (result.Success)
            {
                TempData["Success"] = $"Import complete: {result.Data!.SuccessCount} succeeded, {result.Data.FailCount} failed of {result.Data.TotalRows} total";
                if (result.Data.Errors.Any())
                    TempData["ImportErrors"] = string.Join("|", result.Data.Errors.Take(20));
            }
            else TempData["Error"] = result.ErrorMessage;
            return RedirectToAction("Index");
        }
    }

    // ═══════════════════════════════════════════
    // 7. SYSTEM HEALTH CONTROLLER
    // ═══════════════════════════════════════════
    [Authorize(Roles = "Admin")]
    public class SystemController : Controller
    {
        private readonly ISystemHealthService _health;

        public SystemController(ISystemHealthService health) => _health = health;

        public async Task<IActionResult> Index()
        {
            var status = await _health.GetHealthStatusAsync();
            return View(status);
        }

        [HttpGet]
        public async Task<IActionResult> HealthJson() => Json(await _health.GetHealthStatusAsync());
    }

    // ═══════════════════════════════════════════
    // 8. HELP CENTER CONTROLLER
    // ═══════════════════════════════════════════
    [Authorize]
    public class HelpController : Controller
    {
        public IActionResult Index() => View();
    }

    // ═══════════════════════════════════════════
    // 9. ATTENDANCE CONTROLLER
    // ═══════════════════════════════════════════
    [Authorize]
    public class AttendanceController : Controller
    {
        private readonly IAttendanceService _attendance;
        private readonly ApplicationDbContext _context;

        public AttendanceController(IAttendanceService attendance, Data.ApplicationDbContext context)
        { _attendance = attendance; _context = context; }

        public async Task<IActionResult> Index(DateTime? date, int? teamLeaderId)
        {
            var d = date ?? DateTime.Today;
            var records = await _attendance.GetAttendanceAsync(d, teamLeaderId);
            var students = await _context.Students.Include(s => s.TeamLeader).ThenInclude(t => t.User)
                .Where(s => s.IsActive).ToListAsync();

            // Team leaders only see their students
            if (User.IsInRole("TeamLeader"))
            {
                var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity!.Name);
                if (currentUser != null)
                {
                    var tl = await _context.TeamLeaders.FirstOrDefaultAsync(t => t.UserId == currentUser.Id && t.IsActive);
                    if (tl != null)
                    {
                        teamLeaderId = tl.Id;
                        students = students.Where(s => s.TeamLeaderId == tl.Id).ToList();
                        records = await _attendance.GetAttendanceAsync(d, tl.Id);
                    }
                }
            }
            else if (teamLeaderId.HasValue)
            {
                students = students.Where(s => s.TeamLeaderId == teamLeaderId.Value).ToList();
            }

            ViewBag.Date = d;
            ViewBag.TeamLeaderId = teamLeaderId;
            ViewBag.Records = records;
            ViewBag.TeamLeaders = await _context.TeamLeaders.Include(t => t.User).Where(t => t.IsActive).ToListAsync();
            var stats = await _attendance.GetAttendanceStatsAsync(d.AddDays(-30), d);
            ViewBag.Stats = stats;
            return View(students);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager,Staff,TeamLeader")]
        public async Task<IActionResult> Mark([FromBody] MarkAttendanceRequest req)
        {
            var result = await _attendance.MarkAttendanceAsync(req.StudentId, req.Date, req.Status, req.Notes, User.Identity?.Name ?? "");
            return Json(new { success = result.Success });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager,Staff,TeamLeader")]
        public async Task<IActionResult> BulkMark([FromBody] BulkMarkRequest req)
        {
            var result = await _attendance.BulkMarkAttendanceAsync(req.StudentIds, req.Date, req.Status, User.Identity?.Name ?? "");
            return Json(new { success = result.Success });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager,Staff,TeamLeader")]
        public async Task<IActionResult> ClockInOnBehalf([FromBody] ProxyClockRequest req)
        {
            var result = await _attendance.ClockInAsync(req.StudentId, null, null, null, null, null, User.Identity?.Name, true);
            return Json(new { success = result.Success, message = result.ErrorMessage });
        }

        [HttpGet]
        public async Task<IActionResult> Stats(DateTime from, DateTime to, int? studentId) =>
            Json(await _attendance.GetAttendanceStatsAsync(from, to, studentId));
    }

    public class MarkAttendanceRequest { public int StudentId { get; set; } public DateTime Date { get; set; } public string Status { get; set; } = "Present"; public string? Notes { get; set; } }
    public class BulkMarkRequest { public List<int> StudentIds { get; set; } = new(); public DateTime Date { get; set; } public string Status { get; set; } = "Present"; }
    public class ProxyClockRequest { public int StudentId { get; set; } }

    // ═══════════════════════════════════════════
    // 3. STUDENT CARD (API endpoint)
    // ═══════════════════════════════════════════
    [Authorize]
    public class StudentCardController : Controller
    {
        private readonly IStudentCardService _card;

        public StudentCardController(IStudentCardService card) => _card = card;

        [HttpGet]
        public async Task<IActionResult> Get(int id)
        {
            var data = await _card.GetStudentCardAsync(id);
            if (data == null) return NotFound();
            return Json(data);
        }
    }

    // ═══════════════════════════════════════════
    // 10. USER ACTIVITY LOG (Admin only)
    // ═══════════════════════════════════════════
    [Authorize(Roles = "Admin")]
    public class ActivityController : Controller
    {
        private readonly IUserActivityService _activity;
        private readonly UserManager<ApplicationUser> _userManager;

        public ActivityController(IUserActivityService activity, UserManager<ApplicationUser> userManager)
        { _activity = activity; _userManager = userManager; }

        public async Task<IActionResult> Index(string? search, string? activityType, string? userId,
            DateTime? from, DateTime? to, int page = 1)
        {
            var (items, totalCount, totalPages) = await _activity.SearchAsync(search, activityType, userId, from, to, page, 30);
            ViewBag.Search = search;
            ViewBag.ActivityType = activityType;
            ViewBag.UserId = userId;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.Users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FirstName).ToListAsync();
            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> Stats(DateTime? from, DateTime? to)
        {
            var stats = await _activity.GetStatsAsync(from, to);
            return Json(stats);
        }

        [HttpGet]
        public async Task<IActionResult> Timeline(string? userId, int count = 50)
        {
            var items = await _activity.GetTimelineAsync(userId, count);
            return Json(items);
        }
    }
}
