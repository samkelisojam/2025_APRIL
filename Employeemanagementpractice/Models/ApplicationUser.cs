using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Employeemanagementpractice.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? ProfileImageUrl { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsLocked { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

        // PIN for sensitive operations (e.g. 202612345678)
        [MaxLength(20)]
        public string? SecurityPin { get; set; }

        public string FullName => $"{FirstName} {LastName}";

        // Navigation
        public TeamLeader? TeamLeader { get; set; }
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
        public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
        public ICollection<TaskItem> CreatedTasks { get; set; } = new List<TaskItem>();
    }
}
