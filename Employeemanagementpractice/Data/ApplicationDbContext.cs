using Employeemanagementpractice.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Employeemanagementpractice.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<TeamLeader> TeamLeaders { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<StudentDocument> StudentDocuments { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<TaskItem> TaskItems { get; set; }
        public DbSet<TaskComment> TaskComments { get; set; }
        public DbSet<TaskAttachment> TaskAttachments { get; set; }
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<PayrollRecord> PayrollRecords { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<SavedReport> SavedReports { get; set; }
        public DbSet<TrainingCourse> TrainingCourses { get; set; }
        public DbSet<TrainingProgress> TrainingProgress { get; set; }
        public DbSet<BackupRecord> BackupRecords { get; set; }
        public DbSet<CalendarEvent> CalendarEvents { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
        public DbSet<UserActivity> UserActivities { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // TeamLeader - User relationship
            builder.Entity<TeamLeader>()
                .HasOne(t => t.User)
                .WithOne(u => u.TeamLeader)
                .HasForeignKey<TeamLeader>(t => t.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Student - TeamLeader relationship
            builder.Entity<Student>()
                .HasOne(s => s.TeamLeader)
                .WithMany(t => t.Students)
                .HasForeignKey(s => s.TeamLeaderId)
                .OnDelete(DeleteBehavior.Restrict);

            // StudentDocument - Student relationship
            builder.Entity<StudentDocument>()
                .HasOne(d => d.Student)
                .WithMany(s => s.Documents)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            // TaskItem - assigned user
            builder.Entity<TaskItem>()
                .HasOne(t => t.AssignedTo)
                .WithMany(u => u.AssignedTasks)
                .HasForeignKey(t => t.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // TaskItem - created by user
            builder.Entity<TaskItem>()
                .HasOne(t => t.CreatedBy)
                .WithMany(u => u.CreatedTasks)
                .HasForeignKey(t => t.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // AuditLog - User
            builder.Entity<AuditLog>()
                .HasOne(a => a.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // PayrollRecord - Student
            builder.Entity<PayrollRecord>()
                .HasOne(p => p.Student)
                .WithMany(s => s.PayrollRecords)
                .HasForeignKey(p => p.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            // RolePermission - Permission
            builder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany()
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            builder.Entity<Student>().HasIndex(s => s.SAIDNumber).IsUnique();
            builder.Entity<TeamLeader>().HasIndex(t => t.EmployeeNumber).IsUnique();
            builder.Entity<AuditLog>().HasIndex(a => a.Timestamp);
            builder.Entity<AuditLog>().HasIndex(a => a.UserId);

            // Seed permissions
            builder.Entity<Permission>().HasData(
                new Permission { Id = 1, Name = "ViewDashboard", Description = "View the dashboard", Category = "Dashboard" },
                new Permission { Id = 2, Name = "ManageTeamLeaders", Description = "Create, edit, delete team leaders", Category = "Team Leaders" },
                new Permission { Id = 3, Name = "ViewTeamLeaders", Description = "View team leaders list", Category = "Team Leaders" },
                new Permission { Id = 4, Name = "ManageStudents", Description = "Create, edit, delete students", Category = "Students" },
                new Permission { Id = 5, Name = "ViewStudents", Description = "View students list", Category = "Students" },
                new Permission { Id = 6, Name = "ManageDocuments", Description = "Upload and delete documents", Category = "Documents" },
                new Permission { Id = 7, Name = "ViewDocuments", Description = "View and download documents", Category = "Documents" },
                new Permission { Id = 8, Name = "ManagePayroll", Description = "Create and edit payroll records", Category = "Payroll" },
                new Permission { Id = 9, Name = "ViewPayroll", Description = "View payroll data", Category = "Payroll" },
                new Permission { Id = 10, Name = "ExportData", Description = "Export data to PDF/Excel", Category = "Reports" },
                new Permission { Id = 11, Name = "ViewReports", Description = "View reports", Category = "Reports" },
                new Permission { Id = 12, Name = "ManageReports", Description = "Create and save custom reports", Category = "Reports" },
                new Permission { Id = 13, Name = "ViewAuditLog", Description = "View audit trail", Category = "Audit" },
                new Permission { Id = 14, Name = "ManageTasks", Description = "Create and assign tasks", Category = "Tasks" },
                new Permission { Id = 15, Name = "ViewTasks", Description = "View assigned tasks", Category = "Tasks" },
                new Permission { Id = 16, Name = "ManageAnnouncements", Description = "Create announcements", Category = "Communication" },
                new Permission { Id = 17, Name = "ManageUsers", Description = "Manage user accounts", Category = "Administration" },
                new Permission { Id = 18, Name = "ManageRoles", Description = "Manage roles and permissions", Category = "Administration" },
                new Permission { Id = 19, Name = "ManageSettings", Description = "Manage system settings", Category = "Administration" }
            );

            // Seed role permissions for Admin
            var adminPermissions = new List<RolePermission>();
            for (int i = 1; i <= 19; i++)
            {
                adminPermissions.Add(new RolePermission
                {
                    Id = i,
                    RoleName = "Admin",
                    PermissionId = i,
                    CanView = true,
                    CanCreate = true,
                    CanEdit = true,
                    CanDelete = true
                });
            }
            builder.Entity<RolePermission>().HasData(adminPermissions.ToArray());
        }
    }
}
