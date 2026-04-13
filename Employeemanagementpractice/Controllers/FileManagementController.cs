using Employeemanagementpractice.Models;
using Employeemanagementpractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Employeemanagementpractice.Controllers
{
    [Authorize(Roles = "Admin,Manager,Staff")]
    public class FileManagementController : Controller
    {
        private readonly IFileManagementService _fileManagementService;
        private readonly IEmailService _emailService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;
        private readonly IAuditService _auditService;

        public FileManagementController(
            IFileManagementService fileManagementService,
            IEmailService emailService,
            UserManager<ApplicationUser> userManager,
            IConfiguration config,
            IAuditService auditService)
        {
            _fileManagementService = fileManagementService;
            _emailService = emailService;
            _userManager = userManager;
            _config = config;
            _auditService = auditService;
        }

        private async Task<(string userId, string role)> GetUserContextAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user!);
            return (user!.Id, roles.FirstOrDefault() ?? "ReadOnly");
        }

        public async Task<IActionResult> Index(string? search, int? studentId, int? documentType,
            DateTime? from, DateTime? to, string? uploadedBy, int page = 1, int pageSize = 25)
        {
            var (userId, role) = await GetUserContextAsync();
            var result = await _fileManagementService.SearchDocumentsAsync(
                search, studentId, documentType, from, to, uploadedBy, page, pageSize, userId, role);

            ViewBag.Search = search;
            ViewBag.StudentId = studentId;
            ViewBag.DocumentType = documentType;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");
            ViewBag.UploadedBy = uploadedBy;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = result.TotalPages;
            ViewBag.TotalCount = result.TotalCount;
            ViewBag.TotalSize = result.TotalSize;
            ViewBag.TypeBreakdown = result.TypeBreakdown;

            return View(result.Documents);
        }

        [HttpGet]
        public async Task<IActionResult> Download(int id)
        {
            var (userId, role) = await GetUserContextAsync();
            var (data, contentType, fileName) = await _fileManagementService.GetDocumentAsync(id, userId, role);
            if (data == null) return NotFound();

            await _auditService.LogAsync("Download", "Document", id.ToString(),
                $"Downloaded document: {fileName}", User.Identity?.Name);

            return File(data, contentType!, fileName!);
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(string? search, int? studentId, int? documentType,
            DateTime? from, DateTime? to)
        {
            var (userId, role) = await GetUserContextAsync();
            var data = await _fileManagementService.ExportDocumentListExcelAsync(search, studentId, documentType, from, to, userId, role);
            return File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"DocumentList_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdfData(string? search, int? studentId, int? documentType,
            DateTime? from, DateTime? to)
        {
            var (userId, role) = await GetUserContextAsync();
            var data = await _fileManagementService.ExportDocumentListPdfDataAsync(search, studentId, documentType, from, to, userId, role);
            return Content(System.Text.Encoding.UTF8.GetString(data), "application/json");
        }

        [HttpPost]
        public async Task<IActionResult> BulkDownload([FromBody] BulkActionRequest request)
        {
            if (!await VerifyPin(request.Pin))
                return Json(new { success = false, message = "Invalid security PIN" });

            if (request.DocumentIds == null || request.DocumentIds.Count == 0)
                return Json(new { success = false, message = "No documents selected" });

            var zipData = await _fileManagementService.CreateZipArchiveAsync(request.DocumentIds);

            await _auditService.LogAsync("BulkDownload", "Document", string.Join(",", request.DocumentIds),
                $"Bulk downloaded {request.DocumentIds.Count} documents", User.Identity?.Name);

            return File(zipData, "application/zip", $"Documents_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
        }

        [HttpPost]
        public async Task<IActionResult> SendEmail([FromBody] EmailRequest request)
        {
            if (!await VerifyPin(request.Pin))
                return Json(new { success = false, message = "Invalid security PIN" });

            if (string.IsNullOrWhiteSpace(request.ToEmail) || string.IsNullOrWhiteSpace(request.Subject))
                return Json(new { success = false, message = "Email address and subject are required" });

            byte[]? attachmentData = null;
            string? attachmentName = null;
            string? contentType = null;

            if (request.DocumentId.HasValue)
            {
                var (data, ct, fn) = await _fileManagementService.GetDocumentAsync(request.DocumentId.Value);
                attachmentData = data;
                attachmentName = fn;
                contentType = ct;
            }

            var result = await _emailService.SendEmailAsync(
                request.ToEmail, request.Subject, request.Body ?? "",
                attachmentData, attachmentName, contentType);

            if (result.Success)
            {
                await _auditService.LogAsync("EmailSent", "Document",
                    request.DocumentId?.ToString() ?? "",
                    $"Email sent to {request.ToEmail}: {request.Subject}", User.Identity?.Name);
            }

            return Json(new { success = result.Success, message = result.Success ? "Email sent successfully" : result.ErrorMessage });
        }

        [HttpPost]
        public async Task<IActionResult> VerifyPinEndpoint([FromBody] PinRequest request)
        {
            var valid = await VerifyPin(request.Pin);
            return Json(new { success = valid, message = valid ? "PIN verified" : "Invalid PIN" });
        }

        private async Task<bool> VerifyPin(string? pin)
        {
            if (string.IsNullOrWhiteSpace(pin)) return false;

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return false;

            // Check user's personal PIN first, then fallback to system default
            if (!string.IsNullOrEmpty(user.SecurityPin))
                return user.SecurityPin == pin;

            var defaultPin = _config["SecurityPin:DefaultPin"] ?? "202612345678";
            return pin == defaultPin;
        }
    }

    public class BulkActionRequest
    {
        public List<int> DocumentIds { get; set; } = new();
        public string? Pin { get; set; }
    }

    public class EmailRequest
    {
        public string ToEmail { get; set; } = "";
        public string Subject { get; set; } = "";
        public string? Body { get; set; }
        public int? DocumentId { get; set; }
        public string? Pin { get; set; }
    }

    public class PinRequest
    {
        public string? Pin { get; set; }
    }
}
