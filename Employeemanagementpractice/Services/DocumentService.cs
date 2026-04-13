using Employeemanagementpractice.Data;
using Employeemanagementpractice.Models;
using Microsoft.EntityFrameworkCore;

namespace Employeemanagementpractice.Services
{
    public interface IDocumentService
    {
        Task<(byte[]? Data, string? ContentType, string? OriginalFileName)> DownloadAsync(int id);
        Task<ServiceResult> DeleteAsync(int id);
        Task<ServiceResult<int>> UploadAsync(int studentId, int documentType, IFormFile file, string uploadedBy);
    }

    public class DocumentService : IDocumentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileStorageService _fileStorage;

        public DocumentService(ApplicationDbContext context, IFileStorageService fileStorage)
        {
            _context = context;
            _fileStorage = fileStorage;
        }

        public async Task<(byte[]? Data, string? ContentType, string? OriginalFileName)> DownloadAsync(int id)
        {
            var doc = await _context.StudentDocuments.FindAsync(id);
            if (doc == null) return (null, null, null);

            var data = await _fileStorage.GetFileAsync(doc.FtpPath, doc.FileData, doc.StorageType);
            return (data, doc.ContentType ?? "application/octet-stream", doc.OriginalFileName);
        }

        public async Task<ServiceResult> DeleteAsync(int id)
        {
            var doc = await _context.StudentDocuments.FindAsync(id);
            if (doc == null)
                return ServiceResult.Fail("Document not found.");

            await _fileStorage.DeleteFileAsync(doc.FtpPath, doc.StorageType);
            _context.StudentDocuments.Remove(doc);
            await _context.SaveChangesAsync();

            return ServiceResult.Ok();
        }

        public async Task<ServiceResult<int>> UploadAsync(int studentId, int documentType, IFormFile file, string uploadedBy)
        {
            if (file == null || file.Length == 0)
                return ServiceResult<int>.Fail("Please select a file to upload.");

            var (data, ftpPath, storageType) = await _fileStorage.SaveFileAsync(file, $"documents/{studentId}");

            var doc = new StudentDocument
            {
                StudentId = studentId,
                DocumentType = (DocumentType)documentType,
                FileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}",
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                StorageType = storageType,
                FileData = storageType == StorageType.Database ? data : null,
                FtpPath = ftpPath,
                IsMandatory = documentType <= 2,
                UploadedBy = uploadedBy
            };

            _context.StudentDocuments.Add(doc);
            await _context.SaveChangesAsync();

            return ServiceResult<int>.Ok(doc.Id);
        }
    }
}
