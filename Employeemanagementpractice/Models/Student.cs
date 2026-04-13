using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employeemanagementpractice.Models
{
    public enum Gender
    {
        Male,
        Female,
        Other
    }

    public enum Province
    {
        EasternCape,
        FreeState,
        Gauteng,
        KwaZuluNatal,
        Limpopo,
        Mpumalanga,
        NorthWest,
        NorthernCape,
        WesternCape
    }

    public enum Race
    {
        Black,
        White,
        Coloured,
        Indian,
        Asian,
        Other
    }

    public enum MaritalStatus
    {
        Single,
        Married,
        Divorced,
        Widowed,
        Other
    }

    public enum DisabilityStatus
    {
        None,
        Physical,
        Visual,
        Hearing,
        Intellectual,
        Other
    }

    public class Student
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TeamLeaderId { get; set; }

        [ForeignKey("TeamLeaderId")]
        public TeamLeader TeamLeader { get; set; } = null!;

        // --- Personal Information ---
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

        public DateTime? DateOfBirth { get; set; }

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

        // --- Contact Information ---
        [MaxLength(200), EmailAddress]
        public string? Email { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(20)]
        public string? AlternativePhone { get; set; }

        [MaxLength(20)]
        public string? WhatsAppNumber { get; set; }

        // --- Physical Address ---
        [MaxLength(200)]
        public string? StreetAddress { get; set; }

        [MaxLength(200)]
        public string? Suburb { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        public Province? Province { get; set; }

        [MaxLength(10)]
        public string? PostalCode { get; set; }

        // --- Postal Address ---
        [MaxLength(200)]
        public string? PostalAddress { get; set; }

        [MaxLength(100)]
        public string? PostalCity { get; set; }

        public Province? PostalProvince { get; set; }

        [MaxLength(10)]
        public string? PostalPostalCode { get; set; }

        public bool SameAsPhysical { get; set; }

        // --- Qualifications ---
        [MaxLength(200)]
        public string? QualificationType { get; set; }

        [MaxLength(300)]
        public string? QualificationName { get; set; }

        [MaxLength(300)]
        public string? Institution { get; set; }

        public int? YearCompleted { get; set; }

        [MaxLength(100)]
        public string? StudentNumber { get; set; }

        [MaxLength(200)]
        public string? HighestGradePass { get; set; }

        [MaxLength(500)]
        public string? OtherQualifications { get; set; }

        // --- Work Experience ---
        [MaxLength(200)]
        public string? PreviousEmployer { get; set; }

        [MaxLength(200)]
        public string? PreviousJobTitle { get; set; }

        public int? YearsExperience { get; set; }

        [MaxLength(1000)]
        public string? WorkExperienceDescription { get; set; }

        [MaxLength(500)]
        public string? Skills { get; set; }

        [MaxLength(300)]
        public string? DriversLicense { get; set; }

        public bool HasOwnTransport { get; set; }

        // --- Next of Kin ---
        [MaxLength(200)]
        public string? NextOfKinName { get; set; }

        [MaxLength(100)]
        public string? NextOfKinRelationship { get; set; }

        [MaxLength(20)]
        public string? NextOfKinPhone { get; set; }

        [MaxLength(20)]
        public string? NextOfKinAlternativePhone { get; set; }

        [MaxLength(200), EmailAddress]
        public string? NextOfKinEmail { get; set; }

        [MaxLength(500)]
        public string? NextOfKinAddress { get; set; }

        // --- Bank Details ---
        [MaxLength(100)]
        public string? BankName { get; set; }

        [MaxLength(50)]
        public string? BankAccountNumber { get; set; }

        [MaxLength(20)]
        public string? BankBranchCode { get; set; }

        [MaxLength(100)]
        public string? BranchName { get; set; }

        [MaxLength(50)]
        public string? AccountType { get; set; }

        [MaxLength(200)]
        public string? AccountHolderName { get; set; }

        // --- Profile Image ---
        [MaxLength(500)]
        public string? ProfileImageUrl { get; set; }

        public byte[]? ProfileImageData { get; set; }

        // --- System Fields ---
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }

        // Legacy compat
        [NotMapped]
        public string? Address => StreetAddress;

        public string FullName => $"{FirstName} {LastName}";

        // Navigation
        public ICollection<StudentDocument> Documents { get; set; } = new List<StudentDocument>();
        public ICollection<PayrollRecord> PayrollRecords { get; set; } = new List<PayrollRecord>();
    }
}
