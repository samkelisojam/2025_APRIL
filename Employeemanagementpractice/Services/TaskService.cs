using Employeemanagementpractice.Data;
using Employeemanagementpractice.Models;
using Employeemanagementpractice.Models.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Employeemanagementpractice.Services
{
    public interface ITaskService
    {
        Task<(List<TaskItem> Items, int TotalCount, int TotalPages)> SearchAsync(string? status, string? priority, string? currentUserId, bool isAdmin, int page, int pageSize);
        Task<TaskItem?> GetDetailsAsync(int id);
        Task<ServiceResult<int>> CreateAsync(TaskViewModel model, string currentUserId, string currentUserName);
        Task<ServiceResult> UpdateStatusAsync(int id, Models.TaskStatus status, string currentUserId, string currentUserName);
        Task<ServiceResult> AddCommentAsync(int taskId, string comment, IFormFile? attachment, string currentUserId, string currentUserName);
        Task<(byte[]? Data, string? ContentType, string? FileName)> GetAttachmentAsync(int id);
        Task<List<SelectListItem>> GetUserSelectListAsync();
    }

    public class TaskService : ITaskService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;
        private readonly IFileStorageService _fileStorage;

        public TaskService(ApplicationDbContext context, IAuditService audit, IFileStorageService fileStorage)
        {
            _context = context;
            _audit = audit;
            _fileStorage = fileStorage;
        }

        public async Task<(List<TaskItem> Items, int TotalCount, int TotalPages)> SearchAsync(
            string? status, string? priority, string? currentUserId, bool isAdmin, int page, int pageSize)
        {
            var query = _context.TaskItems.Include(t => t.AssignedTo).Include(t => t.CreatedBy)
                .Include(t => t.Comments).Include(t => t.Attachments).AsQueryable();

            if (!isAdmin)
                query = query.Where(t => t.AssignedToUserId == currentUserId || t.CreatedByUserId == currentUserId);

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Models.TaskStatus>(status, out var st))
                query = query.Where(t => t.Status == st);

            if (!string.IsNullOrWhiteSpace(priority) && Enum.TryParse<TaskPriority>(priority, out var pr))
                query = query.Where(t => t.Priority == pr);

            var count = await query.CountAsync();
            var items = await query.OrderByDescending(t => t.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var totalPages = (int)Math.Ceiling(count / (double)pageSize);

            return (items, count, totalPages);
        }

        public async Task<TaskItem?> GetDetailsAsync(int id)
        {
            return await _context.TaskItems.Include(t => t.AssignedTo).Include(t => t.CreatedBy)
                .Include(t => t.Comments).Include(t => t.Attachments)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<ServiceResult<int>> CreateAsync(TaskViewModel model, string currentUserId, string currentUserName)
        {
            var task = new TaskItem
            {
                Title = model.Title,
                Description = model.Description,
                AssignedToUserId = model.AssignedToUserId,
                CreatedByUserId = currentUserId,
                Priority = model.Priority,
                DueDate = model.DueDate
            };

            _context.TaskItems.Add(task);
            await _context.SaveChangesAsync();

            if (model.Attachment != null)
            {
                var (data, ftpPath, storageType) = await _fileStorage.SaveFileAsync(model.Attachment, $"tasks/{task.Id}");
                _context.TaskAttachments.Add(new TaskAttachment
                {
                    TaskItemId = task.Id,
                    FileName = $"{Guid.NewGuid()}{Path.GetExtension(model.Attachment.FileName)}",
                    OriginalFileName = model.Attachment.FileName,
                    ContentType = model.Attachment.ContentType,
                    StorageType = storageType,
                    FileData = storageType == StorageType.Database ? data : null,
                    FtpPath = ftpPath,
                    UploadedBy = currentUserName
                });
                await _context.SaveChangesAsync();
            }

            await _audit.LogAsync(currentUserId, currentUserName, "Create", "Task",
                task.Id.ToString(), description: $"Created task: {model.Title}");

            return ServiceResult<int>.Ok(task.Id);
        }

        public async Task<ServiceResult> UpdateStatusAsync(int id, Models.TaskStatus status, string currentUserId, string currentUserName)
        {
            var task = await _context.TaskItems.FindAsync(id);
            if (task == null)
                return ServiceResult.Fail("Task not found.");

            task.Status = status;
            task.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, currentUserName, "Update", "Task",
                id.ToString(), description: $"Updated task status to: {status}");

            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> AddCommentAsync(int taskId, string comment, IFormFile? attachment,
            string currentUserId, string currentUserName)
        {
            _context.TaskComments.Add(new TaskComment
            {
                TaskItemId = taskId,
                UserId = currentUserId,
                UserName = currentUserName,
                Comment = comment
            });

            if (attachment != null)
            {
                var (data, ftpPath, storageType) = await _fileStorage.SaveFileAsync(attachment, $"tasks/{taskId}");
                _context.TaskAttachments.Add(new TaskAttachment
                {
                    TaskItemId = taskId,
                    FileName = $"{Guid.NewGuid()}{Path.GetExtension(attachment.FileName)}",
                    OriginalFileName = attachment.FileName,
                    ContentType = attachment.ContentType,
                    StorageType = storageType,
                    FileData = storageType == StorageType.Database ? data : null,
                    FtpPath = ftpPath,
                    UploadedBy = currentUserName
                });
            }

            await _context.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<(byte[]? Data, string? ContentType, string? FileName)> GetAttachmentAsync(int id)
        {
            var att = await _context.TaskAttachments.FindAsync(id);
            if (att == null) return (null, null, null);

            var data = await _fileStorage.GetFileAsync(att.FtpPath, att.FileData, att.StorageType);
            return (data, att.ContentType ?? "application/octet-stream", att.OriginalFileName ?? att.FileName);
        }

        public async Task<List<SelectListItem>> GetUserSelectListAsync()
        {
            return await _context.Users.Where(u => u.IsActive)
                .Select(u => new SelectListItem { Value = u.Id, Text = u.FirstName + " " + u.LastName })
                .ToListAsync();
        }
    }
}
