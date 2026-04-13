using System.ComponentModel.DataAnnotations;

namespace Employeemanagementpractice.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }

        public string? OtpCode { get; set; }
    }

    public class RegisterUserViewModel
    {
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, Phone]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required, MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required, Compare("Password", ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "Staff";
    }

    public class TeamLeaderViewModel
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string EmployeeNumber { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Department { get; set; }

        public int MaxStudents { get; set; } = 25;

        // For creating new team leader with user
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone]
        public string? PhoneNumber { get; set; }

        [DataType(DataType.Password)]
        public string? Password { get; set; }
    }

    public class StudentViewModel
    {
        public int Id { get; set; }

        [Required]
        public int TeamLeaderId { get; set; }

        // Personal
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? MiddleName { get; set; }

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? PreferredName { get; set; }

        [MaxLength(20)]
        public string? Title { get; set; }

        [Required, MaxLength(13)]
        public string SAIDNumber { get; set; } = string.Empty;

        public Gender Gender { get; set; }

        public Race? Race { get; set; }

        [MaxLength(100)]
        public string? Nationality { get; set; }

        [MaxLength(100)]
        public string? HomeLanguage { get; set; }

        public MaritalStatus? MaritalStatus { get; set; }

        public DisabilityStatus? DisabilityStatus { get; set; }

        [MaxLength(300)]
        public string? DisabilityDescription { get; set; }

        // Contact
        [EmailAddress]
        public string? Email { get; set; }

        [Phone]
        public string? Phone { get; set; }

        [Phone]
        public string? AlternativePhone { get; set; }

        [Phone]
        public string? WhatsAppNumber { get; set; }

        // Physical Address
        public string? StreetAddress { get; set; }
        public string? Suburb { get; set; }
        public string? City { get; set; }
        public Province? Province { get; set; }
        public string? PostalCode { get; set; }

        // Postal Address
        public string? PostalAddress { get; set; }
        public string? PostalCity { get; set; }
        public Province? PostalProvince { get; set; }
        public string? PostalPostalCode { get; set; }
        public bool SameAsPhysical { get; set; }

        // Qualifications
        public string? QualificationType { get; set; }
        public string? QualificationName { get; set; }
        public string? Institution { get; set; }
        public int? YearCompleted { get; set; }
        public string? StudentNumber { get; set; }
        public string? HighestGradePass { get; set; }
        public string? OtherQualifications { get; set; }

        // Work Experience
        public string? PreviousEmployer { get; set; }
        public string? PreviousJobTitle { get; set; }
        public int? YearsExperience { get; set; }
        public string? WorkExperienceDescription { get; set; }
        public string? Skills { get; set; }
        public string? DriversLicense { get; set; }
        public bool HasOwnTransport { get; set; }

        // Next of Kin
        public string? NextOfKinName { get; set; }
        public string? NextOfKinRelationship { get; set; }
        public string? NextOfKinPhone { get; set; }
        public string? NextOfKinAlternativePhone { get; set; }
        public string? NextOfKinEmail { get; set; }
        public string? NextOfKinAddress { get; set; }

        // Bank Details
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankBranchCode { get; set; }
        public string? BranchName { get; set; }
        public string? AccountType { get; set; }
        public string? AccountHolderName { get; set; }

        public string? Notes { get; set; }

        // File uploads
        public IFormFile? ProfileImage { get; set; }
        public IFormFile? IDCopyFile { get; set; }
        public IFormFile? QualificationFile { get; set; }
        public IFormFile? BankStatementFile { get; set; }
        public IFormFile? Other1File { get; set; }
        public IFormFile? Other2File { get; set; }
        public IFormFile? Other3File { get; set; }
        public IFormFile? Other4File { get; set; }
    }

    public class DashboardViewModel
    {
        public int TotalTeamLeaders { get; set; }
        public int TotalStudents { get; set; }
        public int ActiveStudents { get; set; }
        public int PendingTasks { get; set; }
        public int TotalPayrollRecords { get; set; }
        public decimal TotalPayrollAmount { get; set; }
        public List<Announcement> RecentAnnouncements { get; set; } = new();
        public List<TaskItem> RecentTasks { get; set; } = new();
        public List<TeamLeaderSummary> TeamLeaderSummaries { get; set; } = new();
    }

    public class TeamLeaderSummary
    {
        public int TeamLeaderId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public int StudentCount { get; set; }
        public int MaxStudents { get; set; }
    }

    public class ReportBuilderViewModel
    {
        public List<ReportField> AvailableFields { get; set; } = new();
        public List<string> SelectedFields { get; set; } = new();
        public string? FilterJson { get; set; }
        public string? ReportName { get; set; }
        public int? SavedReportId { get; set; }
        public List<SavedReport> SavedReports { get; set; } = new();
    }

    public class ReportField
    {
        public string FieldName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Table { get; set; } = string.Empty;
        public string DataType { get; set; } = "string";
    }

    public class TaskViewModel
    {
        public int Id { get; set; }

        [Required, MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public string AssignedToUserId { get; set; } = string.Empty;

        public TaskPriority Priority { get; set; } = TaskPriority.Medium;

        public DateTime? DueDate { get; set; }

        public IFormFile? Attachment { get; set; }
    }

    public class AnnouncementViewModel
    {
        public int Id { get; set; }

        [Required, MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        public TargetAudience TargetAudience { get; set; } = TargetAudience.All;

        public DateTime? ExpiresAt { get; set; }
    }

    public class PayrollViewModel
    {
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; } = DateTime.Today;

        [Required]
        public decimal Amount { get; set; }

        public string? PaymentMethod { get; set; }
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public string? PayPeriod { get; set; }
    }

    public class UserSettingsViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public List<string> Roles { get; set; } = new();
        public string? SelectedRole { get; set; }
    }

    public class AccessControlViewModel
    {
        public List<RolePermissionGroup> RolePermissions { get; set; } = new();
        public List<Permission> AllPermissions { get; set; } = new();
        public List<string> AllRoles { get; set; } = new();
    }

    public class RolePermissionGroup
    {
        public string RoleName { get; set; } = string.Empty;
        public List<RolePermission> Permissions { get; set; } = new();
    }

    public class PaginatedList<T>
    {
        public List<T> Items { get; set; } = new();
        public int PageIndex { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        public PaginatedList() { }

        public PaginatedList(List<T> items, int count, int pageIndex, int pageSize)
        {
            PageIndex = pageIndex;
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);
            TotalCount = count;
            PageSize = pageSize;
            Items = items;
        }
    }
}
