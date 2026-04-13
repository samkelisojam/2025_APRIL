using Employeemanagementpractice.Data;
using Employeemanagementpractice.Models;
using Employeemanagementpractice.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;

namespace Employeemanagementpractice.Services
{
    public interface IReportService
    {
        Task<ReportBuilderViewModel> GetReportBuilderAsync();
        Task<List<TeamLeader>> GetStudentsByTeamLeaderAsync();
        Task<(List<Student> Items, int TotalCount, int TotalPages, List<TeamLeader> TeamLeaders)> GetStudentReportAsync(
            string? search, int? teamLeaderId, int page, int pageSize);
        Task<object> RunCustomReportAsync(List<string> selectedFields);
        Task<ServiceResult<int>> SaveReportAsync(string reportName, string fieldsJson, string? filtersJson, string currentUserId, string currentUserName);
        Task<object?> LoadReportAsync(int id);
        Task<byte[]> ExportStudentsExcelAsync(int? teamLeaderId);
        Task<byte[]> ExportPayrollExcelAsync(int? studentId, string? payPeriod);
    }

    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;

        public ReportService(ApplicationDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<ReportBuilderViewModel> GetReportBuilderAsync()
        {
            return new ReportBuilderViewModel
            {
                AvailableFields = GetAvailableFields(),
                SavedReports = await _context.SavedReports.OrderByDescending(r => r.CreatedAt).ToListAsync()
            };
        }

        public async Task<List<TeamLeader>> GetStudentsByTeamLeaderAsync()
        {
            return await _context.TeamLeaders.Include(t => t.User)
                .Include(t => t.Students.Where(s => s.IsActive))
                .Where(t => t.IsActive).OrderBy(t => t.User.LastName).ToListAsync();
        }

        public async Task<(List<Student> Items, int TotalCount, int TotalPages, List<TeamLeader> TeamLeaders)> GetStudentReportAsync(
            string? search, int? teamLeaderId, int page, int pageSize)
        {
            var query = _context.Students.Include(s => s.TeamLeader).ThenInclude(t => t.User).Where(s => s.IsActive);

            if (teamLeaderId.HasValue) query = query.Where(s => s.TeamLeaderId == teamLeaderId.Value);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(st => st.FirstName.ToLower().Contains(s) || st.LastName.ToLower().Contains(s) || st.SAIDNumber.Contains(s));
            }

            var count = await query.CountAsync();
            var items = await query.OrderBy(s => s.LastName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var totalPages = (int)Math.Ceiling(count / (double)pageSize);
            var teamLeaders = await _context.TeamLeaders.Include(t => t.User).Where(t => t.IsActive).ToListAsync();

            return (items, count, totalPages, teamLeaders);
        }

        public async Task<object> RunCustomReportAsync(List<string> selectedFields)
        {
            var query = _context.Students.Include(s => s.TeamLeader).ThenInclude(t => t.User)
                .Include(s => s.PayrollRecords).Where(s => s.IsActive);

            var results = await query.ToListAsync();

            var data = results.Select(s =>
            {
                var row = new Dictionary<string, object?>();
                foreach (var f in selectedFields)
                {
                    row[f] = f switch
                    {
                        "StudentId" => s.Id,
                        "FirstName" => s.FirstName,
                        "MiddleName" => s.MiddleName,
                        "LastName" => s.LastName,
                        "PreferredName" => s.PreferredName,
                        "SAIDNumber" => s.SAIDNumber,
                        "Email" => s.Email,
                        "Phone" => s.Phone,
                        "AlternativePhone" => s.AlternativePhone,
                        "WhatsAppNumber" => s.WhatsAppNumber,
                        "StreetAddress" => s.StreetAddress,
                        "Suburb" => s.Suburb,
                        "City" => s.City,
                        "Province" => s.Province?.ToString(),
                        "PostalCode" => s.PostalCode,
                        "Gender" => s.Gender.ToString(),
                        "Race" => s.Race?.ToString(),
                        "Nationality" => s.Nationality,
                        "HomeLanguage" => s.HomeLanguage,
                        "MaritalStatus" => s.MaritalStatus?.ToString(),
                        "DateOfBirth" => s.DateOfBirth?.ToString("yyyy-MM-dd"),
                        "QualificationType" => s.QualificationType,
                        "QualificationName" => s.QualificationName,
                        "Institution" => s.Institution,
                        "YearCompleted" => s.YearCompleted,
                        "StudentNumber" => s.StudentNumber,
                        "HighestGradePass" => s.HighestGradePass,
                        "PreviousEmployer" => s.PreviousEmployer,
                        "PreviousJobTitle" => s.PreviousJobTitle,
                        "YearsExperience" => s.YearsExperience,
                        "Skills" => s.Skills,
                        "NextOfKinName" => s.NextOfKinName,
                        "NextOfKinPhone" => s.NextOfKinPhone,
                        "NextOfKinRelationship" => s.NextOfKinRelationship,
                        "BankName" => s.BankName,
                        "BankAccountNumber" => s.BankAccountNumber,
                        "BankBranchCode" => s.BankBranchCode,
                        "BranchName" => s.BranchName,
                        "AccountType" => s.AccountType,
                        "AccountHolderName" => s.AccountHolderName,
                        "TeamLeaderName" => s.TeamLeader?.User?.FullName,
                        "Department" => s.TeamLeader?.Department,
                        "TotalPayments" => s.PayrollRecords?.Sum(p => p.Amount),
                        "LastPaymentDate" => s.PayrollRecords?.OrderByDescending(p => p.PaymentDate).FirstOrDefault()?.PaymentDate.ToString("yyyy-MM-dd"),
                        "CreatedAt" => s.CreatedAt.ToString("yyyy-MM-dd"),
                        _ => null
                    };
                }
                return row;
            }).ToList();

            return new { fields = selectedFields, data };
        }

        public async Task<ServiceResult<int>> SaveReportAsync(string reportName, string fieldsJson, string? filtersJson,
            string currentUserId, string currentUserName)
        {
            var report = new SavedReport
            {
                ReportName = reportName,
                FieldsJson = fieldsJson,
                FiltersJson = filtersJson,
                CreatedBy = currentUserName
            };
            _context.SavedReports.Add(report);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, currentUserName, "Create", "SavedReport",
                report.Id.ToString(), description: $"Saved report: {reportName}");

            return ServiceResult<int>.Ok(report.Id);
        }

        public async Task<object?> LoadReportAsync(int id)
        {
            var report = await _context.SavedReports.FindAsync(id);
            if (report == null) return null;
            return new { report.ReportName, report.FieldsJson, report.FiltersJson };
        }

        public async Task<byte[]> ExportStudentsExcelAsync(int? teamLeaderId)
        {
            var query = _context.Students.Include(s => s.TeamLeader).ThenInclude(t => t.User).Where(s => s.IsActive);
            if (teamLeaderId.HasValue) query = query.Where(s => s.TeamLeaderId == teamLeaderId.Value);

            var students = await query.OrderBy(s => s.LastName).ToListAsync();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Students");

            var headers = new[] { "ID", "First Name", "Last Name", "SA ID Number", "Gender", "Date of Birth",
                "Email", "Phone", "Address", "City", "Province", "Postal Code",
                "Qualification Type", "Qualification", "Institution",
                "Bank", "Account No", "Branch Code", "Account Type",
                "Team Leader", "Department", "Created Date" };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#0d6efd");
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }

            int row = 2;
            foreach (var s in students)
            {
                ws.Cell(row, 1).Value = s.Id;
                ws.Cell(row, 2).Value = s.FirstName;
                ws.Cell(row, 3).Value = s.LastName;
                ws.Cell(row, 4).Value = s.SAIDNumber;
                ws.Cell(row, 5).Value = s.Gender.ToString();
                ws.Cell(row, 6).Value = s.DateOfBirth?.ToString("yyyy-MM-dd") ?? "";
                ws.Cell(row, 7).Value = s.Email ?? "";
                ws.Cell(row, 8).Value = s.Phone ?? "";
                ws.Cell(row, 9).Value = s.Address ?? "";
                ws.Cell(row, 10).Value = s.City ?? "";
                ws.Cell(row, 11).Value = s.Province?.ToString() ?? "";
                ws.Cell(row, 12).Value = s.PostalCode ?? "";
                ws.Cell(row, 13).Value = s.QualificationType ?? "";
                ws.Cell(row, 14).Value = s.QualificationName ?? "";
                ws.Cell(row, 15).Value = s.Institution ?? "";
                ws.Cell(row, 16).Value = s.BankName ?? "";
                ws.Cell(row, 17).Value = s.BankAccountNumber ?? "";
                ws.Cell(row, 18).Value = s.BankBranchCode ?? "";
                ws.Cell(row, 19).Value = s.AccountType ?? "";
                ws.Cell(row, 20).Value = s.TeamLeader?.User?.FullName ?? "";
                ws.Cell(row, 21).Value = s.TeamLeader?.Department ?? "";
                ws.Cell(row, 22).Value = s.CreatedAt.ToString("yyyy-MM-dd");
                row++;
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        public async Task<byte[]> ExportPayrollExcelAsync(int? studentId, string? payPeriod)
        {
            var query = _context.PayrollRecords.Include(p => p.Student)
                .ThenInclude(s => s.TeamLeader).ThenInclude(t => t.User).AsQueryable();
            if (studentId.HasValue) query = query.Where(p => p.StudentId == studentId.Value);
            if (!string.IsNullOrWhiteSpace(payPeriod)) query = query.Where(p => p.PayPeriod == payPeriod);

            var records = await query.OrderByDescending(p => p.PaymentDate).ToListAsync();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Payroll");

            var headers = new[] { "ID", "Student Name", "SA ID", "Team Leader", "Payment Date", "Amount", "Method", "Reference", "Status", "Pay Period", "Notes" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#198754");
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }

            int r = 2;
            foreach (var p in records)
            {
                ws.Cell(r, 1).Value = p.Id;
                ws.Cell(r, 2).Value = p.Student?.FullName ?? "";
                ws.Cell(r, 3).Value = p.Student?.SAIDNumber ?? "";
                ws.Cell(r, 4).Value = p.Student?.TeamLeader?.User?.FullName ?? "";
                ws.Cell(r, 5).Value = p.PaymentDate.ToString("yyyy-MM-dd");
                ws.Cell(r, 6).Value = p.Amount;
                ws.Cell(r, 7).Value = p.PaymentMethod ?? "";
                ws.Cell(r, 8).Value = p.Reference ?? "";
                ws.Cell(r, 9).Value = p.Status.ToString();
                ws.Cell(r, 10).Value = p.PayPeriod ?? "";
                ws.Cell(r, 11).Value = p.Notes ?? "";
                r++;
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        private List<ReportField> GetAvailableFields()
        {
            return new List<ReportField>
            {
                new() { FieldName = "StudentId", DisplayName = "Student ID", Table = "Student", DataType = "int" },
                new() { FieldName = "FirstName", DisplayName = "First Name", Table = "Student" },
                new() { FieldName = "MiddleName", DisplayName = "Middle Name", Table = "Student" },
                new() { FieldName = "LastName", DisplayName = "Last Name", Table = "Student" },
                new() { FieldName = "PreferredName", DisplayName = "Preferred Name", Table = "Student" },
                new() { FieldName = "SAIDNumber", DisplayName = "SA ID Number", Table = "Student" },
                new() { FieldName = "Email", DisplayName = "Email", Table = "Student" },
                new() { FieldName = "Phone", DisplayName = "Phone", Table = "Student" },
                new() { FieldName = "AlternativePhone", DisplayName = "Alternative Phone", Table = "Student" },
                new() { FieldName = "WhatsAppNumber", DisplayName = "WhatsApp", Table = "Student" },
                new() { FieldName = "Gender", DisplayName = "Gender", Table = "Student" },
                new() { FieldName = "Race", DisplayName = "Race", Table = "Student" },
                new() { FieldName = "Nationality", DisplayName = "Nationality", Table = "Student" },
                new() { FieldName = "HomeLanguage", DisplayName = "Home Language", Table = "Student" },
                new() { FieldName = "MaritalStatus", DisplayName = "Marital Status", Table = "Student" },
                new() { FieldName = "DateOfBirth", DisplayName = "Date of Birth", Table = "Student", DataType = "date" },
                new() { FieldName = "StreetAddress", DisplayName = "Street Address", Table = "Student" },
                new() { FieldName = "Suburb", DisplayName = "Suburb", Table = "Student" },
                new() { FieldName = "City", DisplayName = "City", Table = "Student" },
                new() { FieldName = "Province", DisplayName = "Province", Table = "Student" },
                new() { FieldName = "PostalCode", DisplayName = "Postal Code", Table = "Student" },
                new() { FieldName = "QualificationType", DisplayName = "Qualification Type", Table = "Qualification" },
                new() { FieldName = "QualificationName", DisplayName = "Qualification Name", Table = "Qualification" },
                new() { FieldName = "Institution", DisplayName = "Institution", Table = "Qualification" },
                new() { FieldName = "YearCompleted", DisplayName = "Year Completed", Table = "Qualification", DataType = "int" },
                new() { FieldName = "StudentNumber", DisplayName = "Student Number", Table = "Qualification" },
                new() { FieldName = "HighestGradePass", DisplayName = "Highest Grade", Table = "Qualification" },
                new() { FieldName = "PreviousEmployer", DisplayName = "Previous Employer", Table = "Experience" },
                new() { FieldName = "PreviousJobTitle", DisplayName = "Previous Job Title", Table = "Experience" },
                new() { FieldName = "YearsExperience", DisplayName = "Years Experience", Table = "Experience", DataType = "int" },
                new() { FieldName = "Skills", DisplayName = "Skills", Table = "Experience" },
                new() { FieldName = "NextOfKinName", DisplayName = "Next of Kin Name", Table = "Next of Kin" },
                new() { FieldName = "NextOfKinPhone", DisplayName = "Next of Kin Phone", Table = "Next of Kin" },
                new() { FieldName = "NextOfKinRelationship", DisplayName = "Next of Kin Relationship", Table = "Next of Kin" },
                new() { FieldName = "BankName", DisplayName = "Bank Name", Table = "Banking" },
                new() { FieldName = "BankAccountNumber", DisplayName = "Account Number", Table = "Banking" },
                new() { FieldName = "BankBranchCode", DisplayName = "Branch Code", Table = "Banking" },
                new() { FieldName = "BranchName", DisplayName = "Branch Name", Table = "Banking" },
                new() { FieldName = "AccountType", DisplayName = "Account Type", Table = "Banking" },
                new() { FieldName = "AccountHolderName", DisplayName = "Account Holder", Table = "Banking" },
                new() { FieldName = "TeamLeaderName", DisplayName = "Team Leader", Table = "TeamLeader" },
                new() { FieldName = "Department", DisplayName = "Department", Table = "TeamLeader" },
                new() { FieldName = "TotalPayments", DisplayName = "Total Payments", Table = "Payroll", DataType = "decimal" },
                new() { FieldName = "LastPaymentDate", DisplayName = "Last Payment Date", Table = "Payroll", DataType = "date" },
                new() { FieldName = "CreatedAt", DisplayName = "Registration Date", Table = "Student", DataType = "date" },
            };
        }
    }
}
