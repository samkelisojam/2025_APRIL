using Employeemanagementpractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Employeemanagementpractice.Controllers
{
    [Authorize(Roles = "Admin,Manager,Staff")]
    public class AuditController : Controller
    {
        private readonly IAuditLogService _auditLogService;

        public AuditController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        public async Task<IActionResult> Index(string? search, string? action, string? entityType,
            DateTime? from, DateTime? to, int page = 1, int pageSize = 25)
        {
            var (items, totalCount, totalPages, actions, entityTypes) =
                await _auditLogService.SearchAsync(search, action, entityType, from, to, page, pageSize);

            ViewBag.Search = search;
            ViewBag.Action = action;
            ViewBag.EntityType = entityType;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.Actions = actions;
            ViewBag.EntityTypes = entityTypes;

            return View(items);
        }

        public async Task<IActionResult> Details(long id)
        {
            var log = await _auditLogService.GetDetailsAsync(id);
            if (log == null) return NotFound();
            return View(log);
        }
    }
}
