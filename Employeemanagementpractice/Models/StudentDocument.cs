using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employeemanagementpractice.Models
{
    public enum DocumentType
    {
        IDCopy = 0,
        Qualification = 1,
        BankStatement = 2,
        Other1 = 3,
        Other2 = 4,
        Other3 = 5,
        Other4 = 6
    }

    public enum StorageType
    {
        Database = 0,
        FTP = 1
    }

    public class StudentDocument
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public Student Student { get; set; } = null!;

        [Required]
        public DocumentType DocumentType { get; set; }

        [Required, MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ContentType { get; set; }

        public long FileSize { get; set; }

        public StorageType StorageType { get; set; } = StorageType.Database;

        public byte[]? FileData { get; set; }

        [MaxLength(500)]
        public string? FtpPath { get; set; }

        public bool IsMandatory { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(200)]
        public string? UploadedBy { get; set; }

        public string DocumentTypeName => DocumentType switch
        {
            DocumentType.IDCopy => "ID Copy",
            DocumentType.Qualification => "Qualification",
            DocumentType.BankStatement => "Bank Statement",
            DocumentType.Other1 => "Other Document 1",
            DocumentType.Other2 => "Other Document 2",
            DocumentType.Other3 => "Other Document 3",
            DocumentType.Other4 => "Other Document 4",
            _ => "Unknown"
        };
    }
}
