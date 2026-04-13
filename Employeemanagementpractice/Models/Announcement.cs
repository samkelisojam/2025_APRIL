using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employeemanagementpractice.Models
{
    public enum TargetAudience
    {
        All = 0,
        TeamLeaders = 1,
        Students = 2,
        Staff = 3
    }

    public class Announcement
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        [Required]
        public string CreatedByUserId { get; set; } = string.Empty;

        [ForeignKey("CreatedByUserId")]
        public ApplicationUser CreatedBy { get; set; } = null!;

        public TargetAudience TargetAudience { get; set; } = TargetAudience.All;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiresAt { get; set; }
    }
}
