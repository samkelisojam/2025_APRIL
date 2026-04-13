using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employeemanagementpractice.Models
{
    // ═══════════════════════════════════════════
    // 1. TRAINING MODULE
    // ═══════════════════════════════════════════
    public class TrainingCourse
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [MaxLength(100)]
        public string? Category { get; set; }

        [MaxLength(50)]
        public string Difficulty { get; set; } = "Beginner"; // Beginner, Intermediate, Advanced

        public int DurationMinutes { get; set; }

        [MaxLength(500)]
        public string? VideoUrl { get; set; }

        [MaxLength(500)]
        public string? MaterialUrl { get; set; }

        public string? Content { get; set; } // Rich HTML content

        public bool IsActive { get; set; } = true;
        public bool IsMandatory { get; set; }

        public int SortOrder { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(200)]
        public string? CreatedBy { get; set; }

        public ICollection<TrainingProgress> Progress { get; set; } = new List<TrainingProgress>();
    }

    public class TrainingProgress
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CourseId { get; set; }

        [ForeignKey("CourseId")]
        public TrainingCourse Course { get; set; } = null!;

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; } = null!;

        public int ProgressPercent { get; set; } // 0-100

        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

        public int? QuizScore { get; set; }
    }

    // ═══════════════════════════════════════════
    // 2. DATABASE BACKUP
    // ═══════════════════════════════════════════
    public class BackupRecord
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(500)]
        public string FileName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? FilePath { get; set; }

        public long FileSizeBytes { get; set; }

        [MaxLength(50)]
        public string BackupType { get; set; } = "Manual"; // Manual, Scheduled, Monthly

        [MaxLength(50)]
        public string Status { get; set; } = "Completed"; // InProgress, Completed, Failed

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(200)]
        public string? CreatedBy { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }
    }

    // ═══════════════════════════════════════════
    // 5. CALENDAR / EVENTS
    // ═══════════════════════════════════════════
    public class CalendarEvent
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        [MaxLength(200)]
        public string? Location { get; set; }

        [MaxLength(50)]
        public string EventType { get; set; } = "General"; // General, Training, Meeting, Deadline, Holiday

        [MaxLength(20)]
        public string Color { get; set; } = "#0d6efd"; // Bootstrap primary

        public bool IsAllDay { get; set; }

        [Required]
        public string CreatedByUserId { get; set; } = string.Empty;

        [ForeignKey("CreatedByUserId")]
        public ApplicationUser CreatedBy { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ═══════════════════════════════════════════
    // 9. ATTENDANCE TRACKING
    // ═══════════════════════════════════════════
    public class AttendanceRecord
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public Student Student { get; set; } = null!;

        [Required]
        public DateTime Date { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Present"; // Present, Absent, Late, Excused, HalfDay

        public TimeSpan? CheckInTime { get; set; }
        public TimeSpan? CheckOutTime { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(200)]
        public string? MarkedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ═══════════════════════════════════════════
    // 10. USER ACTIVITY TRACKING
    // ═══════════════════════════════════════════
    public class UserActivity
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        [MaxLength(200)]
        public string? UserName { get; set; }

        [MaxLength(200)]
        public string? FullName { get; set; }

        [MaxLength(50)]
        public string? Role { get; set; }

        [Required, MaxLength(50)]
        public string ActivityType { get; set; } = string.Empty; // Login, Logout, PageView, Download, Create, Edit, Delete, Export

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(300)]
        public string? PageUrl { get; set; }

        [MaxLength(100)]
        public string? Controller { get; set; }

        [MaxLength(100)]
        public string? ActionName { get; set; }

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        [MaxLength(300)]
        public string? Browser { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public long? DurationMs { get; set; }
    }
}
