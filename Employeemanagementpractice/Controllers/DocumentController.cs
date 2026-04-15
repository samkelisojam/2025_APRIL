using Employeemanagementpractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Employeemanagementpractice.Controllers
{
    [Authorize]
    public class DocumentController : Controller
    {
        private readonly IDocumentService _documentService;

        public DocumentController(IDocumentService documentService)
        {
            _documentService = documentService;
        }

        public async Task<IActionResult> Download(int id)
        {
            var (data, contentType, originalFileName) = await _documentService.DownloadAsync(id);
            if (data == null) return NotFound();
            return File(data, contentType!, originalFileName!);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager,Staff,TeamLeader")]
        public async Task<IActionResult> Delete(int id, int studentId)
        {
            var result = await _documentService.DeleteAsync(id);
            if (!result.Success) return NotFound();

            TempData["Success"] = "Document deleted successfully!";
            return RedirectToAction("Profile", "Student", new { id = studentId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager,Staff,TeamLeader")]
        public async Task<IActionResult> Upload(int studentId, int documentType, IFormFile file)
        {
            var result = await _documentService.UploadAsync(studentId, documentType, file, User.Identity?.Name);

            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction("Profile", "Student", new { id = studentId });
            }

            TempData["Success"] = "Document uploaded successfully!";
            return RedirectToAction("Profile", "Student", new { id = studentId });
        }
    }
}
