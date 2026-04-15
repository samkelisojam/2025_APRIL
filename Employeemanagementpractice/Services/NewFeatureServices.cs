using Employeemanagementpractice.Data;
using Employeemanagementpractice.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using TaskStatusEnum = Employeemanagementpractice.Models.TaskStatus;

namespace Employeemanagementpractice.Services
{
    // ═══════════════════════════════════════════
    // 1. TRAINING SERVICE
    // ═══════════════════════════════════════════
    public interface ITrainingService
    {
        Task<List<TrainingCourse>> GetCoursesAsync(string? category = null);
        Task<TrainingCourse?> GetCourseAsync(int id);
        Task<ServiceResult<int>> CreateCourseAsync(TrainingCourse course);
        Task<ServiceResult> UpdateCourseAsync(TrainingCourse course);
        Task<ServiceResult> DeleteCourseAsync(int id);
        Task<List<TrainingProgress>> GetUserProgressAsync(string userId);
        Task<ServiceResult> UpdateProgressAsync(int courseId, string userId, int percent);
        Task<ServiceResult> CompleteCourseAsync(int courseId, string userId, int? quizScore);
        Task<object> GetTrainingStatsAsync();
    }

    public class TrainingService : ITrainingService
    {
        private readonly ApplicationDbContext _context;

        public TrainingService(ApplicationDbContext context) => _context = context;

        public async Task<List<TrainingCourse>> GetCoursesAsync(string? category = null)
        {
            var q = _context.TrainingCourses.Where(c => c.IsActive).AsQueryable();
            if (!string.IsNullOrEmpty(category)) q = q.Where(c => c.Category == category);
            return await q.OrderBy(c => c.SortOrder).ThenBy(c => c.Title).ToListAsync();
        }

        public async Task<TrainingCourse?> GetCourseAsync(int id) =>
            await _context.TrainingCourses.Include(c => c.Progress).FirstOrDefaultAsync(c => c.Id == id);

        public async Task<ServiceResult<int>> CreateCourseAsync(TrainingCourse course)
        {
            _context.TrainingCourses.Add(course);
            await _context.SaveChangesAsync();
            return new ServiceResult<int> { Success = true, Data = course.Id };
        }

        public async Task<ServiceResult> UpdateCourseAsync(TrainingCourse course)
        {
            _context.TrainingCourses.Update(course);
            await _context.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> DeleteCourseAsync(int id)
        {
            var c = await _context.TrainingCourses.FindAsync(id);
            if (c == null) return ServiceResult.Fail("Course not found");
            c.IsActive = false;
            await _context.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<List<TrainingProgress>> GetUserProgressAsync(string userId) =>
            await _context.TrainingProgress.Include(p => p.Course)
                .Where(p => p.UserId == userId).OrderByDescending(p => p.LastAccessedAt).ToListAsync();

        public async Task<ServiceResult> UpdateProgressAsync(int courseId, string userId, int percent)
        {
            var p = await _context.TrainingProgress.FirstOrDefaultAsync(x => x.CourseId == courseId && x.UserId == userId);
            if (p == null)
            {
                p = new TrainingProgress { CourseId = courseId, UserId = userId, ProgressPercent = percent };
                _context.TrainingProgress.Add(p);
            }
            else
            {
                p.ProgressPercent = percent;
                p.LastAccessedAt = DateTime.UtcNow;
                if (percent >= 100) { p.IsCompleted = true; p.CompletedAt = DateTime.UtcNow; }
            }
            await _context.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> CompleteCourseAsync(int courseId, string userId, int? quizScore)
        {
            var p = await _context.TrainingProgress.FirstOrDefaultAsync(x => x.CourseId == courseId && x.UserId == userId);
            if (p == null) p = new TrainingProgress { CourseId = courseId, UserId = userId };
            p.IsCompleted = true; p.CompletedAt = DateTime.UtcNow; p.ProgressPercent = 100; p.QuizScore = quizScore;
            if (p.Id == 0) _context.TrainingProgress.Add(p);
            await _context.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<object> GetTrainingStatsAsync()
        {
            var totalCourses = await _context.TrainingCourses.CountAsync(c => c.IsActive);
            var totalCompleted = await _context.TrainingProgress.CountAsync(p => p.IsCompleted);
            var totalInProgress = await _context.TrainingProgress.CountAsync(p => !p.IsCompleted);
            var avgScore = await _context.TrainingProgress.Where(p => p.QuizScore.HasValue).AverageAsync(p => (double?)p.QuizScore) ?? 0;
            return new { totalCourses, totalCompleted, totalInProgress, avgScore = Math.Round(avgScore, 1) };
        }
    }

    // ═══════════════════════════════════════════
    // 2. BACKUP SERVICE
    // ═══════════════════════════════════════════
    public interface IBackupService
    {
        Task<ServiceResult<BackupRecord>> CreateBackupAsync(string backupType, string createdBy);
        Task<List<BackupRecord>> GetBackupHistoryAsync();
        Task<(byte[]? Data, string? FileName)> DownloadBackupAsync(int id);
    }

    public class BackupService : IBackupService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<BackupService> _logger;

        public BackupService(ApplicationDbContext context, IConfiguration config, IWebHostEnvironment env, ILogger<BackupService> logger)
        { _context = context; _config = config; _env = env; _logger = logger; }

        public async Task<ServiceResult<BackupRecord>> CreateBackupAsync(string backupType, string createdBy)
        {
            try
            {
                var backupFolder = Path.Combine(_env.ContentRootPath, "Backups");
                Directory.CreateDirectory(backupFolder);

                var fileName = $"EMS_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                var filePath = Path.Combine(backupFolder, fileName);

                // SQL Server backup command
                var connStr = _config.GetConnectionString("DefaultConnection");
                var dbName = "employeeDB_PRACTISE";
                try
                {
                    await _context.Database.ExecuteSqlAsync(
                        $"BACKUP DATABASE [{dbName}] TO DISK = {filePath} WITH FORMAT, INIT, NAME = 'EMS Backup'");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SQL BACKUP command failed (may need permissions). Creating CSV export instead.");
                    // Fallback: export key data as Excel
                    filePath = Path.Combine(backupFolder, $"EMS_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                    fileName = Path.GetFileName(filePath);
                    await CreateExcelBackupAsync(filePath);
                }

                var fileSize = File.Exists(filePath) ? new FileInfo(filePath).Length : 0;
                var record = new BackupRecord
                {
                    FileName = fileName, FilePath = filePath, FileSizeBytes = fileSize,
                    BackupType = backupType, Status = "Completed", CreatedBy = createdBy
                };
                _context.BackupRecords.Add(record);
                await _context.SaveChangesAsync();
                return new ServiceResult<BackupRecord> { Success = true, Data = record };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup failed");
                return new ServiceResult<BackupRecord> { Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task CreateExcelBackupAsync(string filePath)
        {
            using var wb = new XLWorkbook();
            // Students
            var students = await _context.Students.Include(s => s.TeamLeader).ThenInclude(t => t.User).ToListAsync();
            var ws1 = wb.Worksheets.Add("Students");
            ws1.Cell(1, 1).Value = "ID"; ws1.Cell(1, 2).Value = "FirstName"; ws1.Cell(1, 3).Value = "LastName";
            ws1.Cell(1, 4).Value = "SAIDNumber"; ws1.Cell(1, 5).Value = "Email"; ws1.Cell(1, 6).Value = "Phone";
            ws1.Cell(1, 7).Value = "City"; ws1.Cell(1, 8).Value = "Province"; ws1.Cell(1, 9).Value = "TeamLeader";
            ws1.Cell(1, 10).Value = "BankName"; ws1.Cell(1, 11).Value = "BankAccount";
            for (int i = 0; i < students.Count; i++)
            {
                var s = students[i]; var r = i + 2;
                ws1.Cell(r, 1).Value = s.Id; ws1.Cell(r, 2).Value = s.FirstName; ws1.Cell(r, 3).Value = s.LastName;
                ws1.Cell(r, 4).Value = s.SAIDNumber; ws1.Cell(r, 5).Value = s.Email ?? ""; ws1.Cell(r, 6).Value = s.Phone ?? "";
                ws1.Cell(r, 7).Value = s.City ?? ""; ws1.Cell(r, 8).Value = s.Province?.ToString() ?? "";
                ws1.Cell(r, 9).Value = s.TeamLeader?.User?.FullName ?? ""; ws1.Cell(r, 10).Value = s.BankName ?? "";
                ws1.Cell(r, 11).Value = s.BankAccountNumber ?? "";
            }
            ws1.Columns().AdjustToContents();

            // Payroll
            var payroll = await _context.PayrollRecords.Include(p => p.Student).ToListAsync();
            var ws2 = wb.Worksheets.Add("Payroll");
            ws2.Cell(1, 1).Value = "Student"; ws2.Cell(1, 2).Value = "Amount"; ws2.Cell(1, 3).Value = "Date";
            ws2.Cell(1, 4).Value = "Status"; ws2.Cell(1, 5).Value = "Reference";
            for (int i = 0; i < payroll.Count; i++)
            {
                var p = payroll[i]; var r = i + 2;
                ws2.Cell(r, 1).Value = p.Student?.FullName ?? ""; ws2.Cell(r, 2).Value = p.Amount;
                ws2.Cell(r, 3).Value = p.PaymentDate.ToString("yyyy-MM-dd"); ws2.Cell(r, 4).Value = p.Status.ToString();
                ws2.Cell(r, 5).Value = p.Reference ?? "";
            }
            ws2.Columns().AdjustToContents();

            // Attendance
            var attendance = await _context.AttendanceRecords.Include(a => a.Student).OrderByDescending(a => a.Date).ToListAsync();
            var ws3 = wb.Worksheets.Add("Attendance");
            ws3.Cell(1, 1).Value = "Student"; ws3.Cell(1, 2).Value = "Date"; ws3.Cell(1, 3).Value = "Status";
            ws3.Cell(1, 4).Value = "Clock In"; ws3.Cell(1, 5).Value = "Clock Out"; ws3.Cell(1, 6).Value = "Hours Worked";
            ws3.Cell(1, 7).Value = "Clock In Address"; ws3.Cell(1, 8).Value = "Clock In Device";
            ws3.Cell(1, 9).Value = "Clock Out Address"; ws3.Cell(1, 10).Value = "Clock Out Device";
            ws3.Cell(1, 11).Value = "Proxy"; ws3.Cell(1, 12).Value = "Marked By";
            for (int i = 0; i < attendance.Count; i++)
            {
                var a = attendance[i]; var r = i + 2;
                ws3.Cell(r, 1).Value = a.Student?.FullName ?? ""; ws3.Cell(r, 2).Value = a.Date.ToString("yyyy-MM-dd");
                ws3.Cell(r, 3).Value = a.Status ?? ""; ws3.Cell(r, 4).Value = a.ClockInTime?.ToString("HH:mm") ?? "";
                ws3.Cell(r, 5).Value = a.ClockOutTime?.ToString("HH:mm") ?? "";
                ws3.Cell(r, 6).Value = a.HoursWorked?.ToString("F1") ?? "";
                ws3.Cell(r, 7).Value = a.ClockInAddress ?? ""; ws3.Cell(r, 8).Value = a.ClockInDeviceName ?? "";
                ws3.Cell(r, 9).Value = a.ClockOutAddress ?? ""; ws3.Cell(r, 10).Value = a.ClockOutDeviceName ?? "";
                ws3.Cell(r, 11).Value = a.IsMarkedByProxy ? "Yes" : "No"; ws3.Cell(r, 12).Value = a.MarkedBy ?? "";
            }
            ws3.Columns().AdjustToContents();

            // Daily Diaries
            var diaries = await _context.DailyDiaries.Include(d => d.Student).OrderByDescending(d => d.Date).ToListAsync();
            var ws4 = wb.Worksheets.Add("Daily Diaries");
            ws4.Cell(1, 1).Value = "Student"; ws4.Cell(1, 2).Value = "Date"; ws4.Cell(1, 3).Value = "Activities";
            ws4.Cell(1, 4).Value = "Achievements"; ws4.Cell(1, 5).Value = "Challenges"; ws4.Cell(1, 6).Value = "Planned For Tomorrow";
            ws4.Cell(1, 7).Value = "Supervisor Comment";
            for (int i = 0; i < diaries.Count; i++)
            {
                var d = diaries[i]; var r = i + 2;
                ws4.Cell(r, 1).Value = d.Student?.FullName ?? ""; ws4.Cell(r, 2).Value = d.Date.ToString("yyyy-MM-dd");
                ws4.Cell(r, 3).Value = d.Activities ?? ""; ws4.Cell(r, 4).Value = d.Achievements ?? "";
                ws4.Cell(r, 5).Value = d.Challenges ?? ""; ws4.Cell(r, 6).Value = d.PlannedForTomorrow ?? "";
                ws4.Cell(r, 7).Value = d.SupervisorComment ?? "";
            }
            ws4.Columns().AdjustToContents();

            wb.SaveAs(filePath);
        }

        public async Task<List<BackupRecord>> GetBackupHistoryAsync() =>
            await _context.BackupRecords.OrderByDescending(b => b.CreatedAt).Take(50).ToListAsync();

        public async Task<(byte[]? Data, string? FileName)> DownloadBackupAsync(int id)
        {
            var record = await _context.BackupRecords.FindAsync(id);
            if (record?.FilePath == null || !File.Exists(record.FilePath)) return (null, null);
            return (await File.ReadAllBytesAsync(record.FilePath), record.FileName);
        }
    }

    // ═══════════════════════════════════════════
    // 4. GLOBAL SEARCH SERVICE
    // ═══════════════════════════════════════════
    public interface IGlobalSearchService
    {
        Task<object> SearchAsync(string query, int maxResults = 20);
    }

    public class GlobalSearchService : IGlobalSearchService
    {
        private readonly ApplicationDbContext _context;

        public GlobalSearchService(ApplicationDbContext context) => _context = context;

        public async Task<object> SearchAsync(string query, int max = 20)
        {
            var q = query.ToLower();
            var students = await _context.Students.Where(s => s.IsActive &&
                (s.FirstName.ToLower().Contains(q) || s.LastName.ToLower().Contains(q) ||
                 s.SAIDNumber.Contains(q) || (s.Email != null && s.Email.ToLower().Contains(q))))
                .Take(max).Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName, s.SAIDNumber, Type = "Student" }).ToListAsync();

            var teamLeaders = await _context.TeamLeaders.Include(t => t.User)
                .Where(t => t.IsActive && (t.User.FirstName.ToLower().Contains(q) || t.User.LastName.ToLower().Contains(q) || t.EmployeeNumber.Contains(q)))
                .Take(max).Select(t => new { t.Id, Name = t.User.FirstName + " " + t.User.LastName, SAIDNumber = t.EmployeeNumber, Type = "TeamLeader" }).ToListAsync();

            var tasks = await _context.TaskItems.Where(t => t.Title.ToLower().Contains(q))
                .Take(max).Select(t => new { t.Id, Name = t.Title, SAIDNumber = "", Type = "Task" }).ToListAsync();

            var announcements = await _context.Announcements.Where(a => a.IsActive && a.Title.ToLower().Contains(q))
                .Take(max).Select(a => new { a.Id, Name = a.Title, SAIDNumber = "", Type = "Announcement" }).ToListAsync();

            var results = students.Cast<object>().Concat(teamLeaders).Concat(tasks).Concat(announcements).Take(max);
            return new { results, total = students.Count + teamLeaders.Count + tasks.Count + announcements.Count };
        }
    }

    // ═══════════════════════════════════════════
    // 5. CALENDAR SERVICE
    // ═══════════════════════════════════════════
    public interface ICalendarService
    {
        Task<List<CalendarEvent>> GetEventsAsync(DateTime from, DateTime to);
        Task<ServiceResult<int>> CreateEventAsync(CalendarEvent ev);
        Task<ServiceResult> DeleteEventAsync(int id);
        Task<List<object>> GetUpcomingDeadlinesAsync(int days = 14);
    }

    public class CalendarService : ICalendarService
    {
        private readonly ApplicationDbContext _context;

        public CalendarService(ApplicationDbContext context) => _context = context;

        public async Task<List<CalendarEvent>> GetEventsAsync(DateTime from, DateTime to) =>
            await _context.CalendarEvents.Where(e => e.StartDate >= from && e.StartDate <= to)
                .OrderBy(e => e.StartDate).ToListAsync();

        public async Task<ServiceResult<int>> CreateEventAsync(CalendarEvent ev)
        {
            _context.CalendarEvents.Add(ev);
            await _context.SaveChangesAsync();
            return new ServiceResult<int> { Success = true, Data = ev.Id };
        }

        public async Task<ServiceResult> DeleteEventAsync(int id)
        {
            var e = await _context.CalendarEvents.FindAsync(id);
            if (e == null) return ServiceResult.Fail("Event not found");
            _context.CalendarEvents.Remove(e);
            await _context.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<List<object>> GetUpcomingDeadlinesAsync(int days = 14)
        {
            var cutoff = DateTime.UtcNow.AddDays(days);
            var taskDeadlines = await _context.TaskItems
                .Where(t => t.DueDate.HasValue && t.DueDate <= cutoff && t.Status != TaskStatusEnum.Completed && t.Status != TaskStatusEnum.Cancelled)
                .Select(t => new { Title = "Task: " + t.Title, Date = t.DueDate!.Value, Type = "Task", Color = "#dc3545" })
                .ToListAsync();

            var events = await _context.CalendarEvents
                .Where(e => e.StartDate >= DateTime.UtcNow && e.StartDate <= cutoff)
                .Select(e => new { e.Title, Date = e.StartDate, Type = e.EventType, e.Color })
                .ToListAsync();

            return taskDeadlines.Cast<object>().Concat(events).OrderBy(x => ((dynamic)x).Date).ToList();
        }
    }

    // ═══════════════════════════════════════════
    // 6. BATCH IMPORT SERVICE
    // ═══════════════════════════════════════════
    public interface IBatchImportService
    {
        Task<ServiceResult<ImportResult>> ImportStudentsFromExcelAsync(Stream fileStream, string importedBy);
        byte[] GetImportTemplate();
    }

    public class ImportResult
    {
        public int TotalRows { get; set; }
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class BatchImportService : IBatchImportService
    {
        private readonly ApplicationDbContext _context;
        private readonly ISaIdValidationService _saIdService;

        public BatchImportService(ApplicationDbContext context, ISaIdValidationService saIdService)
        { _context = context; _saIdService = saIdService; }

        public async Task<ServiceResult<ImportResult>> ImportStudentsFromExcelAsync(Stream fileStream, string importedBy)
        {
            var result = new ImportResult();
            try
            {
                using var wb = new XLWorkbook(fileStream);
                var ws = wb.Worksheets.First();
                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
                result.TotalRows = lastRow - 1; // exclude header

                for (int row = 2; row <= lastRow; row++)
                {
                    try
                    {
                        var firstName = ws.Cell(row, 1).GetString().Trim();
                        var lastName = ws.Cell(row, 2).GetString().Trim();
                        var saId = ws.Cell(row, 3).GetString().Trim();
                        var email = ws.Cell(row, 4).GetString().Trim();
                        var phone = ws.Cell(row, 5).GetString().Trim();
                        var teamLeaderId = ws.Cell(row, 6).GetString().Trim();

                        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(saId))
                        {
                            result.Errors.Add($"Row {row}: Missing required fields (FirstName, LastName, SAIDNumber)");
                            result.FailCount++; continue;
                        }

                        if (await _context.Students.AnyAsync(s => s.SAIDNumber == saId))
                        {
                            result.Errors.Add($"Row {row}: SA ID {saId} already exists");
                            result.FailCount++; continue;
                        }

                        int tlId = 0;
                        if (!int.TryParse(teamLeaderId, out tlId) || !await _context.TeamLeaders.AnyAsync(t => t.Id == tlId))
                        {
                            var firstTl = await _context.TeamLeaders.FirstOrDefaultAsync(t => t.IsActive);
                            tlId = firstTl?.Id ?? 0;
                            if (tlId == 0) { result.Errors.Add($"Row {row}: No team leader found"); result.FailCount++; continue; }
                        }

                        var student = new Student
                        {
                            FirstName = firstName, LastName = lastName, SAIDNumber = saId,
                            Email = string.IsNullOrEmpty(email) ? null : email,
                            Phone = string.IsNullOrEmpty(phone) ? null : phone,
                            TeamLeaderId = tlId,
                            City = ws.Cell(row, 7).GetString().Trim(),
                            Gender = ws.Cell(row, 8).GetString().Trim().ToLower() == "female" ? Gender.Female : Gender.Male
                        };
                        _context.Students.Add(student);
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Row {row}: {ex.Message}");
                        result.FailCount++;
                    }
                }
                await _context.SaveChangesAsync();
                return new ServiceResult<ImportResult> { Success = true, Data = result };
            }
            catch (Exception ex)
            {
                return new ServiceResult<ImportResult> { Success = false, ErrorMessage = $"Import failed: {ex.Message}" };
            }
        }

        public byte[] GetImportTemplate()
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Students Import");
            var headers = new[] { "FirstName*", "LastName*", "SAIDNumber*", "Email", "Phone", "TeamLeaderId", "City", "Gender (Male/Female)" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#0d6efd");
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }
            // Sample row
            ws.Cell(2, 1).Value = "John"; ws.Cell(2, 2).Value = "Doe"; ws.Cell(2, 3).Value = "9901015800085";
            ws.Cell(2, 4).Value = "john@email.co.za"; ws.Cell(2, 5).Value = "0712345678";
            ws.Cell(2, 6).Value = "1"; ws.Cell(2, 7).Value = "Johannesburg"; ws.Cell(2, 8).Value = "Male";
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }
    }

    // ═══════════════════════════════════════════
    // 7. SYSTEM HEALTH SERVICE
    // ═══════════════════════════════════════════
    public interface ISystemHealthService
    {
        Task<object> GetHealthStatusAsync();
    }

    public class SystemHealthService : ISystemHealthService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public SystemHealthService(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        { _context = context; _userManager = userManager; _env = env; }

        public async Task<object> GetHealthStatusAsync()
        {
            var dbOk = await _context.Database.CanConnectAsync();
            var totalUsers = await _userManager.Users.CountAsync();
            var activeUsers = await _userManager.Users.CountAsync(u => u.IsActive);
            var totalStudents = await _context.Students.CountAsync(s => s.IsActive);
            var totalDocs = await _context.StudentDocuments.CountAsync();
            var docSize = await _context.StudentDocuments.SumAsync(d => d.FileSize);
            var totalTasks = await _context.TaskItems.CountAsync();
            var pendingTasks = await _context.TaskItems.CountAsync(t => t.Status == TaskStatusEnum.New || t.Status == TaskStatusEnum.InProgress);
            var totalPayroll = await _context.PayrollRecords.CountAsync();
            var totalAuditLogs = await _context.AuditLogs.CountAsync();
            var lastBackup = await _context.BackupRecords.OrderByDescending(b => b.CreatedAt).FirstOrDefaultAsync();
            var backupFolder = Path.Combine(_env.ContentRootPath, "Backups");
            var backupSize = Directory.Exists(backupFolder) ? new DirectoryInfo(backupFolder).GetFiles().Sum(f => f.Length) : 0;

            return new
            {
                database = new { connected = dbOk, provider = "SQL Server" },
                users = new { total = totalUsers, active = activeUsers },
                students = new { total = totalStudents },
                documents = new { total = totalDocs, totalSizeMB = Math.Round(docSize / 1048576.0, 2) },
                tasks = new { total = totalTasks, pending = pendingTasks },
                payroll = new { totalRecords = totalPayroll },
                auditLogs = new { total = totalAuditLogs },
                backups = new { lastBackup = lastBackup?.CreatedAt.ToString("yyyy-MM-dd HH:mm") ?? "Never", totalSizeMB = Math.Round(backupSize / 1048576.0, 2) },
                server = new { environment = _env.EnvironmentName, machineName = Environment.MachineName, os = Environment.OSVersion.ToString(), runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, uptime = (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).ToString(@"dd\.hh\:mm\:ss") }
            };
        }
    }

    // ═══════════════════════════════════════════
    // 9. ATTENDANCE SERVICE
    // ═══════════════════════════════════════════
    public interface IAttendanceService
    {
        Task<List<AttendanceRecord>> GetAttendanceAsync(DateTime date, int? teamLeaderId = null);
        Task<ServiceResult> MarkAttendanceAsync(int studentId, DateTime date, string status, string? notes, string markedBy);
        Task<ServiceResult> BulkMarkAttendanceAsync(List<int> studentIds, DateTime date, string status, string markedBy);
        Task<object> GetAttendanceStatsAsync(DateTime from, DateTime to, int? studentId = null);
        Task<ServiceResult<int>> ClockInAsync(int studentId, double? latitude, double? longitude, string? address, string? deviceName, string? selfieUrl, string? markedBy, bool isProxy);
        Task<ServiceResult> ClockOutAsync(int studentId, double? latitude, double? longitude, string? address, string? deviceName, string? selfieUrl);
        Task<AttendanceRecord?> GetTodayRecordAsync(int studentId);
        Task<List<AttendanceRecord>> GetStudentHistoryAsync(int studentId, DateTime from, DateTime to);
        Task<object> GetStudentSummaryAsync(int studentId, DateTime from, DateTime to);
    }

    public class AttendanceService : IAttendanceService
    {
        private readonly ApplicationDbContext _context;

        public AttendanceService(ApplicationDbContext context) => _context = context;

        public async Task<List<AttendanceRecord>> GetAttendanceAsync(DateTime date, int? teamLeaderId = null)
        {
            var q = _context.AttendanceRecords.Include(a => a.Student).ThenInclude(s => s.TeamLeader).ThenInclude(t => t.User)
                .Where(a => a.Date.Date == date.Date);
            if (teamLeaderId.HasValue) q = q.Where(a => a.Student.TeamLeaderId == teamLeaderId.Value);
            return await q.OrderBy(a => a.Student.LastName).ToListAsync();
        }

        public async Task<ServiceResult> MarkAttendanceAsync(int studentId, DateTime date, string status, string? notes, string markedBy)
        {
            var existing = await _context.AttendanceRecords.FirstOrDefaultAsync(a => a.StudentId == studentId && a.Date.Date == date.Date);
            if (existing != null) { existing.Status = status; existing.Notes = notes; existing.MarkedBy = markedBy; existing.IsMarkedByProxy = true; }
            else
            {
                _context.AttendanceRecords.Add(new AttendanceRecord
                {
                    StudentId = studentId, Date = date.Date, Status = status,
                    Notes = notes, MarkedBy = markedBy, IsMarkedByProxy = true,
                    ClockInTime = (status == "Present" || status == "Late") ? SastClock.Now : null,
                    CheckInTime = (status == "Present" || status == "Late") ? SastClock.Now.TimeOfDay : null
                });
            }
            await _context.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> BulkMarkAttendanceAsync(List<int> studentIds, DateTime date, string status, string markedBy)
        {
            foreach (var sid in studentIds)
                await MarkAttendanceAsync(sid, date, status, null, markedBy);
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult<int>> ClockInAsync(int studentId, double? latitude, double? longitude, string? address, string? deviceName, string? selfieUrl, string? markedBy, bool isProxy)
        {
            var today = SastClock.Today;
            var existing = await _context.AttendanceRecords.FirstOrDefaultAsync(a => a.StudentId == studentId && a.Date == today);
            if (existing != null && existing.ClockInTime != null)
                return new ServiceResult<int> { Success = false, ErrorMessage = "Already clocked in today" };

            var now = SastClock.Now;
            if (existing != null)
            {
                existing.ClockInTime = now;
                existing.ClockInLatitude = latitude;
                existing.ClockInLongitude = longitude;
                existing.ClockInAddress = address;
                existing.ClockInDeviceName = deviceName;
                existing.ClockInSelfieUrl = selfieUrl;
                existing.Status = now.Hour >= 9 ? "Late" : "Present";
                existing.CheckInTime = now.TimeOfDay;
                existing.MarkedBy = markedBy;
                existing.IsMarkedByProxy = isProxy;
            }
            else
            {
                existing = new AttendanceRecord
                {
                    StudentId = studentId, Date = today,
                    Status = now.Hour >= 9 ? "Late" : "Present",
                    ClockInTime = now,
                    ClockInLatitude = latitude, ClockInLongitude = longitude,
                    ClockInAddress = address, ClockInDeviceName = deviceName,
                    ClockInSelfieUrl = selfieUrl,
                    CheckInTime = now.TimeOfDay,
                    MarkedBy = markedBy,
                    IsMarkedByProxy = isProxy
                };
                _context.AttendanceRecords.Add(existing);
            }
            await _context.SaveChangesAsync();
            return new ServiceResult<int> { Success = true, Data = existing.Id };
        }

        public async Task<ServiceResult> ClockOutAsync(int studentId, double? latitude, double? longitude, string? address, string? deviceName, string? selfieUrl)
        {
            var today = SastClock.Today;
            var record = await _context.AttendanceRecords.FirstOrDefaultAsync(a => a.StudentId == studentId && a.Date == today);
            if (record == null || record.ClockInTime == null)
                return new ServiceResult { Success = false, ErrorMessage = "Must clock in first" };
            if (record.ClockOutTime != null)
                return new ServiceResult { Success = false, ErrorMessage = "Already clocked out today" };

            var now = SastClock.Now;
            record.ClockOutTime = now;
            record.ClockOutLatitude = latitude;
            record.ClockOutLongitude = longitude;
            record.ClockOutAddress = address;
            record.ClockOutDeviceName = deviceName;
            record.ClockOutSelfieUrl = selfieUrl;
            record.CheckOutTime = now.TimeOfDay;
            record.HoursWorked = Math.Round((now - record.ClockInTime.Value).TotalHours, 2);

            await _context.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<AttendanceRecord?> GetTodayRecordAsync(int studentId)
        {
            return await _context.AttendanceRecords
                .FirstOrDefaultAsync(a => a.StudentId == studentId && a.Date == SastClock.Today);
        }

        public async Task<List<AttendanceRecord>> GetStudentHistoryAsync(int studentId, DateTime from, DateTime to)
        {
            return await _context.AttendanceRecords
                .Where(a => a.StudentId == studentId && a.Date >= from && a.Date <= to)
                .OrderByDescending(a => a.Date)
                .ToListAsync();
        }

        public async Task<object> GetStudentSummaryAsync(int studentId, DateTime from, DateTime to)
        {
            var records = await _context.AttendanceRecords
                .Where(a => a.StudentId == studentId && a.Date >= from && a.Date <= to)
                .ToListAsync();

            var totalDays = records.Count;
            var present = records.Count(a => a.Status == "Present");
            var late = records.Count(a => a.Status == "Late");
            var absent = records.Count(a => a.Status == "Absent");
            var totalHours = records.Where(a => a.HoursWorked.HasValue).Sum(a => a.HoursWorked!.Value);
            var avgHours = records.Any(a => a.HoursWorked.HasValue) ? records.Where(a => a.HoursWorked.HasValue).Average(a => a.HoursWorked!.Value) : 0;

            return new { totalDays, present, late, absent, totalHours = Math.Round(totalHours, 1), avgHoursPerDay = Math.Round(avgHours, 1) };
        }

        public async Task<object> GetAttendanceStatsAsync(DateTime from, DateTime to, int? studentId = null)
        {
            var q = _context.AttendanceRecords.Where(a => a.Date >= from && a.Date <= to);
            if (studentId.HasValue) q = q.Where(a => a.StudentId == studentId.Value);

            var total = await q.CountAsync();
            var present = await q.CountAsync(a => a.Status == "Present");
            var absent = await q.CountAsync(a => a.Status == "Absent");
            var late = await q.CountAsync(a => a.Status == "Late");
            var excused = await q.CountAsync(a => a.Status == "Excused");
            var rate = total > 0 ? Math.Round((present + late) * 100.0 / total, 1) : 0;

            return new { total, present, absent, late, excused, attendanceRate = rate };
        }
    }

    // ═══════════════════════════════════════════
    // 9b. DAILY DIARY SERVICE
    // ═══════════════════════════════════════════
    public interface IDailyDiaryService
    {
        Task<DailyDiary?> GetDiaryAsync(int studentId, DateTime date);
        Task<List<DailyDiary>> GetDiaryHistoryAsync(int studentId, DateTime from, DateTime to);
        Task<ServiceResult> SaveDiaryAsync(int studentId, DateTime date, string? activities, string? achievements, string? challenges, string? plannedForTomorrow);
        Task<ServiceResult> AddSupervisorCommentAsync(int diaryId, string comment);
    }

    public class DailyDiaryService : IDailyDiaryService
    {
        private readonly ApplicationDbContext _context;
        public DailyDiaryService(ApplicationDbContext context) => _context = context;

        public async Task<DailyDiary?> GetDiaryAsync(int studentId, DateTime date)
        {
            return await _context.DailyDiaries
                .Include(d => d.AttendanceRecord)
                .FirstOrDefaultAsync(d => d.StudentId == studentId && d.Date == date.Date);
        }

        public async Task<List<DailyDiary>> GetDiaryHistoryAsync(int studentId, DateTime from, DateTime to)
        {
            return await _context.DailyDiaries
                .Include(d => d.AttendanceRecord)
                .Where(d => d.StudentId == studentId && d.Date >= from && d.Date <= to)
                .OrderByDescending(d => d.Date)
                .ToListAsync();
        }

        public async Task<ServiceResult> SaveDiaryAsync(int studentId, DateTime date, string? activities, string? achievements, string? challenges, string? plannedForTomorrow)
        {
            var existing = await _context.DailyDiaries.FirstOrDefaultAsync(d => d.StudentId == studentId && d.Date == date.Date);
            var attendance = await _context.AttendanceRecords.FirstOrDefaultAsync(a => a.StudentId == studentId && a.Date == date.Date);

            if (existing != null)
            {
                existing.Activities = activities;
                existing.Achievements = achievements;
                existing.Challenges = challenges;
                existing.PlannedForTomorrow = plannedForTomorrow;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.DailyDiaries.Add(new DailyDiary
                {
                    StudentId = studentId, Date = date.Date,
                    AttendanceRecordId = attendance?.Id,
                    Activities = activities, Achievements = achievements,
                    Challenges = challenges, PlannedForTomorrow = plannedForTomorrow
                });
            }
            await _context.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> AddSupervisorCommentAsync(int diaryId, string comment)
        {
            var diary = await _context.DailyDiaries.FindAsync(diaryId);
            if (diary == null) return new ServiceResult { Success = false, ErrorMessage = "Diary not found" };
            diary.SupervisorComment = comment;
            diary.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return ServiceResult.Ok();
        }
    }

    // ═══════════════════════════════════════════
    // 3. STUDENT PROFILE CARD SERVICE (for modal)
    // ═══════════════════════════════════════════
    public interface IStudentCardService
    {
        Task<object?> GetStudentCardAsync(int id);
    }

    public class StudentCardService : IStudentCardService
    {
        private readonly ApplicationDbContext _context;

        public StudentCardService(ApplicationDbContext context) => _context = context;

        public async Task<object?> GetStudentCardAsync(int id)
        {
            var s = await _context.Students
                .Include(x => x.TeamLeader).ThenInclude(t => t.User)
                .Include(x => x.Documents)
                .Include(x => x.PayrollRecords)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return null;

            return new
            {
                s.Id, s.FirstName, s.LastName, s.FullName, s.SAIDNumber,
                s.Title, s.MiddleName, s.PreferredName,
                Gender = s.Gender.ToString(), Race = s.Race?.ToString() ?? "",
                s.Nationality, s.HomeLanguage,
                MaritalStatus = s.MaritalStatus?.ToString() ?? "",
                DisabilityStatus = s.DisabilityStatus?.ToString() ?? "",
                s.DisabilityDescription,
                DateOfBirth = s.DateOfBirth?.ToString("yyyy-MM-dd") ?? "",
                s.Email, s.Phone, s.AlternativePhone, s.WhatsAppNumber,
                s.StreetAddress, s.Suburb, s.City,
                Province = s.Province?.ToString() ?? "", s.PostalCode,
                s.PostalAddress, s.PostalCity,
                PostalProvince = s.PostalProvince?.ToString() ?? "", s.PostalPostalCode,
                s.QualificationType, s.QualificationName, s.Institution,
                s.YearCompleted, s.StudentNumber, s.HighestGradePass, s.OtherQualifications,
                s.PreviousEmployer, s.PreviousJobTitle, s.YearsExperience,
                s.WorkExperienceDescription, s.Skills, s.DriversLicense, s.HasOwnTransport,
                s.NextOfKinName, s.NextOfKinRelationship, s.NextOfKinPhone,
                s.NextOfKinAlternativePhone, s.NextOfKinEmail, s.NextOfKinAddress,
                s.BankName, s.BankAccountNumber, s.BankBranchCode, s.BranchName,
                s.AccountType, s.AccountHolderName,
                s.Notes, s.ProfileImageUrl,
                TeamLeader = s.TeamLeader?.User?.FullName ?? "",
                TeamLeaderDept = s.TeamLeader?.Department ?? "",
                CreatedAt = s.CreatedAt.ToString("yyyy-MM-dd"),
                Documents = s.Documents.Select(d => new {
                    d.Id, d.DocumentTypeName, d.OriginalFileName, d.ContentType,
                    FileSize = d.FileSize < 1048576 ? $"{d.FileSize / 1024.0:F1} KB" : $"{d.FileSize / 1048576.0:F1} MB",
                    UploadedAt = d.UploadedAt.ToString("yyyy-MM-dd")
                }),
                PayrollSummary = new {
                    TotalPayments = s.PayrollRecords.Count,
                    TotalAmount = s.PayrollRecords.Sum(p => p.Amount),
                    LastPayment = s.PayrollRecords.OrderByDescending(p => p.PaymentDate).FirstOrDefault()?.PaymentDate.ToString("yyyy-MM-dd") ?? "N/A"
                }
            };
        }
    }
}
