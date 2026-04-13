using Employeemanagementpractice.Data;
using Employeemanagementpractice.Models;
using Employeemanagementpractice.Models.ViewModels;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace Employeemanagementpractice.Services
{
    public interface IFileManagementService
    {
        Task<FileManagementResult> SearchDocumentsAsync(string? search, int? studentId, int? documentType,
            DateTime? from, DateTime? to, string? uploadedBy, int page, int pageSize,
            string? userId = null, string? userRole = null);
        Task<byte[]> ExportDocumentListExcelAsync(string? search, int? studentId, int? documentType, DateTime? from, DateTime? to,
            string? userId = null, string? userRole = null);
        Task<byte[]> ExportDocumentListPdfDataAsync(string? search, int? studentId, int? documentType, DateTime? from, DateTime? to,
            string? userId = null, string? userRole = null);
        Task<(byte[]? Data, string? ContentType, string? FileName)> GetDocumentAsync(int id, string? userId = null, string? userRole = null);
        Task<List<StudentDocument>> GetDocumentsByIdsAsync(List<int> ids);
        Task<byte[]> CreateZipArchiveAsync(List<int> documentIds);
    }

    public class FileManagementResult
    {
        public List<DocumentListItem> Documents { get; set; } = new();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public long TotalSize { get; set; }
        public Dictionary<string, int> TypeBreakdown { get; set; } = new();
    }

    public class DocumentListItem
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; } = "";
        public string SAIDNumber { get; set; } = "";
        public string TeamLeaderName { get; set; } = "";
        public string DocumentTypeName { get; set; } = "";
        public DocumentType DocumentType { get; set; }
        public string OriginalFileName { get; set; } = "";
        public string? ContentType { get; set; }
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; }
        public string? UploadedBy { get; set; }
        public bool IsMandatory { get; set; }
        public string FileSizeDisplay => FileSize switch
        {
            < 1024 => $"{FileSize} B",
            < 1048576 => $"{FileSize / 1024.0:F1} KB",
            _ => $"{FileSize / 1048576.0:F1} MB"
        };
    }

    public class FileManagementService : IFileManagementService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileStorageService _fileStorage;

        public FileManagementService(ApplicationDbContext context, IFileStorageService fileStorage)
        {
            _context = context;
            _fileStorage = fileStorage;
        }

        private IQueryable<StudentDocument> ApplyDataIsolation(IQueryable<StudentDocument> query, string? userId, string? userRole)
        {
            // Admin and Manager see all; TeamLeader sees only their students; others see none sensitive
            if (userRole == "TeamLeader" && !string.IsNullOrEmpty(userId))
            {
                var teamLeaderIds = _context.TeamLeaders.Where(t => t.UserId == userId).Select(t => t.Id);
                query = query.Where(d => teamLeaderIds.Contains(d.Student.TeamLeaderId));
            }
            else if (userRole == "Staff" || userRole == "ReadOnly")
            {
                // Staff/ReadOnly can view but not sensitive fields - filtered at display level
            }
            // Admin/Manager = no filter
            return query;
        }

        public async Task<FileManagementResult> SearchDocumentsAsync(string? search, int? studentId,
            int? documentType, DateTime? from, DateTime? to, string? uploadedBy, int page, int pageSize,
            string? userId = null, string? userRole = null)
        {
            var query = _context.StudentDocuments
                .Include(d => d.Student).ThenInclude(s => s.TeamLeader).ThenInclude(t => t.User)
                .AsQueryable();

            query = ApplyDataIsolation(query, userId, userRole);

            if (studentId.HasValue)
                query = query.Where(d => d.StudentId == studentId.Value);

            if (documentType.HasValue)
                query = query.Where(d => (int)d.DocumentType == documentType.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(d =>
                    d.OriginalFileName.ToLower().Contains(s) ||
                    d.Student.FirstName.ToLower().Contains(s) ||
                    d.Student.LastName.ToLower().Contains(s) ||
                    d.Student.SAIDNumber.Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(uploadedBy))
                query = query.Where(d => d.UploadedBy != null && d.UploadedBy.ToLower().Contains(uploadedBy.ToLower()));

            if (from.HasValue)
                query = query.Where(d => d.UploadedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(d => d.UploadedAt <= to.Value.AddDays(1));

            var totalCount = await query.CountAsync();
            var totalSize = await query.SumAsync(d => d.FileSize);

            var typeBreakdown = await query.GroupBy(d => d.DocumentType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Type.ToString(), x => x.Count);

            var docs = await query.OrderByDescending(d => d.UploadedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(d => new DocumentListItem
                {
                    Id = d.Id,
                    StudentId = d.StudentId,
                    StudentName = d.Student.FirstName + " " + d.Student.LastName,
                    SAIDNumber = d.Student.SAIDNumber,
                    TeamLeaderName = d.Student.TeamLeader != null ? d.Student.TeamLeader.User.FirstName + " " + d.Student.TeamLeader.User.LastName : "",
                    DocumentTypeName = d.DocumentType == DocumentType.IDCopy ? "ID Copy"
                        : d.DocumentType == DocumentType.Qualification ? "Qualification"
                        : d.DocumentType == DocumentType.BankStatement ? "Bank Statement"
                        : "Other",
                    DocumentType = d.DocumentType,
                    OriginalFileName = d.OriginalFileName,
                    ContentType = d.ContentType,
                    FileSize = d.FileSize,
                    UploadedAt = d.UploadedAt,
                    UploadedBy = d.UploadedBy,
                    IsMandatory = d.IsMandatory
                }).ToListAsync();

            return new FileManagementResult
            {
                Documents = docs,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                TotalSize = totalSize,
                TypeBreakdown = typeBreakdown
            };
        }

        public async Task<byte[]> ExportDocumentListExcelAsync(string? search, int? studentId, int? documentType,
            DateTime? from, DateTime? to, string? userId = null, string? userRole = null)
        {
            var result = await SearchDocumentsAsync(search, studentId, documentType, from, to, null, 1, int.MaxValue, userId, userRole);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Documents");

            var headers = new[] { "ID", "Student Name", "SA ID", "Team Leader", "Document Type", "File Name", "Size", "Uploaded At", "Uploaded By", "Mandatory" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#0d6efd");
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }

            int row = 2;
            foreach (var d in result.Documents)
            {
                ws.Cell(row, 1).Value = d.Id;
                ws.Cell(row, 2).Value = d.StudentName;
                ws.Cell(row, 3).Value = d.SAIDNumber;
                ws.Cell(row, 4).Value = d.TeamLeaderName;
                ws.Cell(row, 5).Value = d.DocumentTypeName;
                ws.Cell(row, 6).Value = d.OriginalFileName;
                ws.Cell(row, 7).Value = d.FileSizeDisplay;
                ws.Cell(row, 8).Value = d.UploadedAt.ToString("yyyy-MM-dd HH:mm");
                ws.Cell(row, 9).Value = d.UploadedBy ?? "";
                ws.Cell(row, 10).Value = d.IsMandatory ? "Yes" : "No";
                row++;
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        public async Task<byte[]> ExportDocumentListPdfDataAsync(string? search, int? studentId, int? documentType,
            DateTime? from, DateTime? to, string? userId = null, string? userRole = null)
        {
            var result = await SearchDocumentsAsync(search, studentId, documentType, from, to, null, 1, 500, userId, userRole);
            var json = System.Text.Json.JsonSerializer.Serialize(result.Documents.Select(d => new
            {
                d.Id, d.StudentName, d.SAIDNumber, d.TeamLeaderName, d.DocumentTypeName,
                d.OriginalFileName, d.FileSizeDisplay, UploadedAt = d.UploadedAt.ToString("yyyy-MM-dd HH:mm"),
                d.UploadedBy, Mandatory = d.IsMandatory ? "Yes" : "No"
            }));
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public async Task<(byte[]? Data, string? ContentType, string? FileName)> GetDocumentAsync(int id,
            string? userId = null, string? userRole = null)
        {
            var query = _context.StudentDocuments.Include(d => d.Student).AsQueryable();
            query = ApplyDataIsolation(query, userId, userRole);

            var doc = await query.FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return (null, null, null);

            var data = await _fileStorage.GetFileAsync(doc.FtpPath, doc.FileData, doc.StorageType);
            return (data, doc.ContentType ?? "application/octet-stream", doc.OriginalFileName);
        }

        public async Task<List<StudentDocument>> GetDocumentsByIdsAsync(List<int> ids)
        {
            return await _context.StudentDocuments.Where(d => ids.Contains(d.Id)).ToListAsync();
        }

        public async Task<byte[]> CreateZipArchiveAsync(List<int> documentIds)
        {
            var docs = await _context.StudentDocuments
                .Include(d => d.Student)
                .Where(d => documentIds.Contains(d.Id))
                .ToListAsync();

            using var memoryStream = new MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                foreach (var doc in docs)
                {
                    var data = await _fileStorage.GetFileAsync(doc.FtpPath, doc.FileData, doc.StorageType);
                    if (data == null) continue;

                    var entryName = $"{doc.Student.FirstName}_{doc.Student.LastName}/{doc.DocumentTypeName}_{doc.OriginalFileName}";
                    var entry = archive.CreateEntry(entryName);
                    using var entryStream = entry.Open();
                    await entryStream.WriteAsync(data, 0, data.Length);
                }
            }

            return memoryStream.ToArray();
        }
    }
}
