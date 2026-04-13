using Employeemanagementpractice.Models;
using Employeemanagementpractice.Models.ViewModels;
using Employeemanagementpractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Employeemanagementpractice.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class PayrollController : Controller
    {
        private readonly IPayrollService _payrollService;
        private readonly UserManager<ApplicationUser> _userManager;

        public PayrollController(IPayrollService payrollService, UserManager<ApplicationUser> userManager)
        {
            _payrollService = payrollService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(int? studentId, string? payPeriod, string? status, int page = 1, int pageSize = 20)
        {
            var (items, totalCount, totalPages, totalAmount) = await _payrollService.SearchAsync(studentId, payPeriod, status, page, pageSize);

            ViewBag.Students = await _payrollService.GetStudentSelectListAsync();
            ViewBag.PayPeriods = await _payrollService.GetPayPeriodsAsync();
            ViewBag.StudentId = studentId;
            ViewBag.PayPeriod = payPeriod;
            ViewBag.Status = status;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalAmount = totalAmount;
            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Students = await _payrollService.GetStudentSelectListAsync();
            return View(new PayrollViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PayrollViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Students = await _payrollService.GetStudentSelectListAsync();
                return View(model);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            await _payrollService.CreateAsync(model, currentUser!.Id, currentUser.FullName);

            TempData["Success"] = "Payroll record created!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, PaymentStatus status)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var result = await _payrollService.UpdateStatusAsync(id, status, currentUser!.Id, currentUser.FullName);
            if (!result.Success) return NotFound();

            return Json(new { success = true });
        }
    }
}
