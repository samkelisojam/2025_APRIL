using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employeemanagementpractice.Models
{
    public enum TaskPriority
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Urgent = 3
    }

    public enum TaskStatus
    {
        New = 0,
        InProgress = 1,
        OnHold = 2,
        Completed = 3,
        Cancelled = 4
    }

    public class TaskItem
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public string AssignedToUserId { get; set; } = string.Empty;

        [ForeignKey("AssignedToUserId")]
        public ApplicationUser AssignedTo { get; set; } = null!;

        [Required]
        public string CreatedByUserId { get; set; } = string.Empty;

        [ForeignKey("CreatedByUserId")]
        public ApplicationUser CreatedBy { get; set; } = null!;

        public TaskPriority Priority { get; set; } = TaskPriority.Medium;
        public TaskStatus Status { get; set; } = TaskStatus.New;

        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation
        public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
        public ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
    }

    public class TaskComment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TaskItemId { get; set; }

        [ForeignKey("TaskItemId")]
        public TaskItem TaskItem { get; set; } = null!;

        [Required]
        public string UserId { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? UserName { get; set; }

        [Required]
        public string Comment { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class TaskAttachment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TaskItemId { get; set; }

        [ForeignKey("TaskItemId")]
        public TaskItem TaskItem { get; set; } = null!;

        [Required, MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? OriginalFileName { get; set; }

        [MaxLength(100)]
        public string? ContentType { get; set; }

        public StorageType StorageType { get; set; } = StorageType.Database;

        public byte[]? FileData { get; set; }

        [MaxLength(500)]
        public string? FtpPath { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(200)]
        public string? UploadedBy { get; set; }
    }
}
