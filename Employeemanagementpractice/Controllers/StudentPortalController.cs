using Employeemanagementpractice.Data;
using Employeemanagementpractice.Models;
using Employeemanagementpractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Employeemanagementpractice.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentPortalController : Controller
    {
        private readonly IAttendanceService _attendance;
        private readonly IDailyDiaryService _diary;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public StudentPortalController(IAttendanceService attendance, IDailyDiaryService diary,
            UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _attendance = attendance;
            _diary = diary;
            _userManager = userManager;
            _context = context;
        }

        private async Task<Student?> GetCurrentStudentAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;
            return await _context.Students
                .Include(s => s.TeamLeader).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(s => s.UserId == user.Id && s.IsActive);
        }

        public async Task<IActionResult> Index()
        {
            var student = await GetCurrentStudentAsync();
            if (student == null) return RedirectToAction("Login", "Account");

            var today = await _attendance.GetTodayRecordAsync(student.Id);
            var diary = await _diary.GetDiaryAsync(student.Id, DateTime.Today);
            var history = await _attendance.GetStudentHistoryAsync(student.Id, DateTime.Today.AddDays(-30), DateTime.Today);
            var summary = await _attendance.GetStudentSummaryAsync(student.Id, DateTime.Today.AddDays(-30), DateTime.Today);

            ViewBag.Student = student;
            ViewBag.TodayRecord = today;
            ViewBag.Diary = diary;
            ViewBag.History = history;
            ViewBag.Summary = summary;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ClockIn([FromBody] ClockRequest req)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null) return Json(new { success = false, message = "Student not found" });

            string? selfieUrl = null;
            if (!string.IsNullOrEmpty(req.SelfieBase64))
            {
                selfieUrl = await SaveSelfieAsync(student.Id, "clockin", req.SelfieBase64);
            }

            var result = await _attendance.ClockInAsync(
                student.Id, req.Latitude, req.Longitude, req.Address,
                req.DeviceName, selfieUrl, null, false);

            return Json(new { success = result.Success, message = result.ErrorMessage, time = DateTime.Now.ToString("HH:mm:ss") });
        }

        [HttpPost]
        public async Task<IActionResult> ClockOut([FromBody] ClockRequest req)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null) return Json(new { success = false, message = "Student not found" });

            string? selfieUrl = null;
            if (!string.IsNullOrEmpty(req.SelfieBase64))
            {
                selfieUrl = await SaveSelfieAsync(student.Id, "clockout", req.SelfieBase64);
            }

            var result = await _attendance.ClockOutAsync(
                student.Id, req.Latitude, req.Longitude, req.Address,
                req.DeviceName, selfieUrl);

            return Json(new { success = result.Success, message = result.ErrorMessage, time = DateTime.Now.ToString("HH:mm:ss") });
        }

        [HttpPost]
        public async Task<IActionResult> SaveDiary([FromBody] DiaryRequest req)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null) return Json(new { success = false });

            var result = await _diary.SaveDiaryAsync(student.Id, DateTime.Today,
                req.Activities, req.Achievements, req.Challenges, req.PlannedForTomorrow);

            return Json(new { success = result.Success });
        }

        [HttpGet]
        public async Task<IActionResult> History(DateTime? from, DateTime? to)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null) return RedirectToAction("Login", "Account");

            var startDate = from ?? DateTime.Today.AddDays(-30);
            var endDate = to ?? DateTime.Today;
            var records = await _attendance.GetStudentHistoryAsync(student.Id, startDate, endDate);
            var diaries = await _diary.GetDiaryHistoryAsync(student.Id, startDate, endDate);
            var summary = await _attendance.GetStudentSummaryAsync(student.Id, startDate, endDate);

            ViewBag.Student = student;
            ViewBag.Records = records;
            ViewBag.Diaries = diaries;
            ViewBag.Summary = summary;
            ViewBag.From = startDate;
            ViewBag.To = endDate;
            return View();
        }

        private async Task<string> SaveSelfieAsync(int studentId, string type, string base64)
        {
            var folder = Path.Combine(Directory.GetCurrentDirectory(), "FtpStorage", "selfies");
            Directory.CreateDirectory(folder);
            var fileName = $"{studentId}_{type}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            var filePath = Path.Combine(folder, fileName);

            // Remove data:image/...;base64, prefix
            var data = base64.Contains(",") ? base64.Split(',')[1] : base64;
            await System.IO.File.WriteAllBytesAsync(filePath, Convert.FromBase64String(data));
            return $"/selfies/{fileName}";
        }
    }

    // ── Request models for Student Portal ──
    public class ClockRequest
    {
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Address { get; set; }
        public string? DeviceName { get; set; }
        public string? SelfieBase64 { get; set; }
    }

    public class DiaryRequest
    {
        public string? Activities { get; set; }
        public string? Achievements { get; set; }
        public string? Challenges { get; set; }
        public string? PlannedForTomorrow { get; set; }
    }
}
