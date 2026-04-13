using Employeemanagementpractice.Models;
using Employeemanagementpractice.Models.ViewModels;
using Employeemanagementpractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Employeemanagementpractice.Controllers
{
    [Authorize(Roles = "Admin,Manager,Staff")]
    public class ReportController : Controller
    {
        private readonly IReportService _reportService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportController(IReportService reportService, UserManager<ApplicationUser> userManager)
        {
            _reportService = reportService;
            _userManager = userManager;
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
                $"Payroll_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }
    }
}
