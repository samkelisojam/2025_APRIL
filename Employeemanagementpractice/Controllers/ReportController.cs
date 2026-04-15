using Employeemanagementpractice.Models;
using Employeemanagementpractice.Models.ViewModels;
using Employeemanagementpractice.Services;
using Employeemanagementpractice.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Employeemanagementpractice.Controllers
{
    [Authorize(Roles = "Admin,Manager,Staff,ReadOnly,TeamLeader")]
    public class ReportController : Controller
    {
        private readonly IReportService _reportService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ReportController(IReportService reportService, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _reportService = reportService;
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var vm = await _reportService.GetReportBuilderAsync();
            return View(vm);
        }

        public async Task<IActionResult> StudentsByTeamLeader()
        {
            var data = await _reportService.GetStudentsByTeamLeaderAsync();
            return View(data);
        }

        public async Task<IActionResult> StudentReport(string? search, int? teamLeaderId, int page = 1, int pageSize = 25)
        {
            var (items, totalCount, totalPages, teamLeaders) = await _reportService.GetStudentReportAsync(search, teamLeaderId, page, pageSize);

            ViewBag.TeamLeaders = teamLeaders;
            ViewBag.Search = search;
            ViewBag.TeamLeaderId = teamLeaderId;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            return View(items);
        }

        [HttpPost]
        public async Task<IActionResult> RunCustomReport([FromBody] ReportBuilderViewModel model)
        {
            var fields = model.SelectedFields ?? new List<string>();
            if (!fields.Any()) return Json(new { error = "Select at least one field" });

            var result = await _reportService.RunCustomReportAsync(fields);
            return Json(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager,Staff")]
        public async Task<IActionResult> SaveReport(string reportName, string fieldsJson, string? filtersJson)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var result = await _reportService.SaveReportAsync(reportName, fieldsJson, filtersJson, currentUser!.Id, currentUser.FullName);
            return Json(new { success = true, id = result.Data });
        }

        [HttpGet]
        public async Task<IActionResult> LoadReport(int id)
        {
            var report = await _reportService.LoadReportAsync(id);
            if (report == null) return NotFound();
            return Json(report);
        }

        public async Task<IActionResult> ExportStudentsExcel(int? teamLeaderId)
        {
            var data = await _reportService.ExportStudentsExcelAsync(teamLeaderId);
            return File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Students_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        public async Task<IActionResult> ExportPayrollExcel(int? studentId, string? payPeriod)
        {
            var data = await _reportService.ExportPayrollExcelAsync(studentId, payPeriod);
            return File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Payroll_{SastClock.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        // ── Monthly Timesheet Excel Export (per Team Leader) ──
        [HttpGet]
        public async Task<IActionResult> ExportTimesheetExcel(int? teamLeaderId, int? year, int? month)
        {
            var y = year ?? SastClock.Now.Year;
            var m = month ?? SastClock.Now.Month;
            var firstDay = new DateTime(y, m, 1);
            var lastDay = firstDay.AddMonths(1).AddDays(-1);
            var daysInMonth = DateTime.DaysInMonth(y, m);

            var studentsQuery = _context.Students.Where(s => s.IsActive);
            if (teamLeaderId.HasValue)
                studentsQuery = studentsQuery.Where(s => s.TeamLeaderId == teamLeaderId.Value);

            // For TeamLeader role, restrict to their students
            if (User.IsInRole("TeamLeader"))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    var tl = await _context.TeamLeaders.FirstOrDefaultAsync(t => t.UserId == currentUser.Id && t.IsActive);
                    if (tl != null) studentsQuery = studentsQuery.Where(s => s.TeamLeaderId == tl.Id);
                }
            }

            var students = await studentsQuery.OrderBy(s => s.LastName).Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName }).ToListAsync();
            var records = await _context.AttendanceRecords
                .Where(a => a.Date >= firstDay && a.Date <= lastDay && a.Student.IsActive)
                .Where(a => !teamLeaderId.HasValue || a.Student.TeamLeaderId == teamLeaderId.Value)
                .ToListAsync();

            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("Timesheet");
            var monthNames = new[] { "", "January","February","March","April","May","June","July","August","September","October","November","December" };
            ws.Cell(1, 1).Value = $"Monthly Timesheet - {monthNames[m]} {y}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            // Headers
            var row = 3;
            ws.Cell(row, 1).Value = "Student";
            for (int d = 1; d <= daysInMonth; d++) ws.Cell(row, d + 1).Value = d;
            ws.Cell(row, daysInMonth + 2).Value = "Total Hours";
            ws.Cell(row, daysInMonth + 3).Value = "Present";
            ws.Cell(row, daysInMonth + 4).Value = "Absent";
            ws.Range(row, 1, row, daysInMonth + 4).Style.Font.Bold = true;

            foreach (var s in students)
            {
                row++;
                ws.Cell(row, 1).Value = s.Name;
                var sr = records.Where(r => r.StudentId == s.Id).ToList();
                for (int d = 1; d <= daysInMonth; d++)
                {
                    var rec = sr.FirstOrDefault(r => r.Date.Day == d);
                    if (rec != null)
                    {
                        var val = rec.Status == "Present" ? "P" : rec.Status == "Late" ? "L" : rec.Status == "Absent" ? "A" : "E";
                        ws.Cell(row, d + 1).Value = val;
                        if (rec.Status == "Present") ws.Cell(row, d + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGreen;
                        else if (rec.Status == "Late") ws.Cell(row, d + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightYellow;
                        else if (rec.Status == "Absent") ws.Cell(row, d + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightCoral;
                    }
                    else ws.Cell(row, d + 1).Value = "-";
                }
                ws.Cell(row, daysInMonth + 2).Value = Math.Round(sr.Where(r => r.HoursWorked.HasValue).Sum(r => r.HoursWorked!.Value), 1);
                ws.Cell(row, daysInMonth + 3).Value = sr.Count(r => r.Status == "Present" || r.Status == "Late");
                ws.Cell(row, daysInMonth + 4).Value = sr.Count(r => r.Status == "Absent");
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Timesheet_{y}_{m:D2}.xlsx");
        }
    }
}
