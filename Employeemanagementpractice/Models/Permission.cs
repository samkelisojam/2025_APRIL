using System.ComponentModel.DataAnnotations;

namespace Employeemanagementpractice.Models
{
    public class Permission
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Description { get; set; }

        [Required, MaxLength(100)]
        public string Category { get; set; } = string.Empty;
    }

    public class RolePermission
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string RoleName { get; set; } = string.Empty;

        [Required]
        public int PermissionId { get; set; }
        public Permission Permission { get; set; } = null!;

        public bool CanView { get; set; }
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }

    public class SavedReport
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string ReportName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        public string FieldsJson { get; set; } = "[]";

        [MaxLength(500)]
        public string? FiltersJson { get; set; }

        [Required]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastRunAt { get; set; }
    }
}
