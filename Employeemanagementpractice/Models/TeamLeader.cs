using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employeemanagementpractice.Models
{
    public class TeamLeader
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; } = null!;

        [Required, MaxLength(50)]
        public string EmployeeNumber { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Department { get; set; }

        public DateTime DateJoined { get; set; } = DateTime.UtcNow;

        public int MaxStudents { get; set; } = 25;

        public bool IsActive { get; set; } = true;

        // Navigation
        public ICollection<Student> Students { get; set; } = new List<Student>();
    }
}
