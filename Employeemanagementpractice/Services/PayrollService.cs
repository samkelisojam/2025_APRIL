using Employeemanagementpractice.Data;
using Employeemanagementpractice.Models;
using Employeemanagementpractice.Models.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Employeemanagementpractice.Services
{
    public interface IPayrollService
    {
        Task<(List<PayrollRecord> Items, int TotalCount, int TotalPages, decimal TotalAmount)> SearchAsync(
            int? studentId, string? payPeriod, string? status, int page, int pageSize);
        Task<ServiceResult<int>> CreateAsync(PayrollViewModel model, string currentUserId, string currentUserName);
        Task<ServiceResult> UpdateStatusAsync(int id, PaymentStatus status, string currentUserId, string currentUserName);
        Task<List<SelectListItem>> GetStudentSelectListAsync();
        Task<List<string>> GetPayPeriodsAsync();
    }

    public class PayrollService : IPayrollService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;

        public PayrollService(ApplicationDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<(List<PayrollRecord> Items, int TotalCount, int TotalPages, decimal TotalAmount)> SearchAsync(
            int? studentId, string? payPeriod, string? status, int page, int pageSize)
        {
            var query = _context.PayrollRecords.Include(p => p.Student)
                .ThenInclude(s => s.TeamLeader).ThenInclude(t => t.User).AsQueryable();

            if (studentId.HasValue) query = query.Where(p => p.StudentId == studentId.Value);
            if (!string.IsNullOrWhiteSpace(payPeriod)) query = query.Where(p => p.PayPeriod == payPeriod);
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<PaymentStatus>(status, out var ps))
                query = query.Where(p => p.Status == ps);

            var count = await query.CountAsync();
            var totalAmount = await query.SumAsync(p => (decimal?)p.Amount) ?? 0;
            var items = await query.OrderByDescending(p => p.PaymentDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var totalPages = (int)Math.Ceiling(count / (double)pageSize);

            return (items, count, totalPages, totalAmount);
        }

        public async Task<ServiceResult<int>> CreateAsync(PayrollViewModel model, string currentUserId, string currentUserName)
        {
            var record = new PayrollRecord
            {
                StudentId = model.StudentId,
                PaymentDate = model.PaymentDate,
                Amount = model.Amount,
                PaymentMethod = model.PaymentMethod,
                Reference = model.Reference,
                Notes = model.Notes,
                PayPeriod = model.PayPeriod,
                CreatedBy = currentUserName
            };

            _context.PayrollRecords.Add(record);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, currentUserName, "Create", "PayrollRecord",
                record.Id.ToString(), description: $"Created payroll record for student {model.StudentId}, Amount: R{model.Amount}");

            return ServiceResult<int>.Ok(record.Id);
        }

        public async Task<ServiceResult> UpdateStatusAsync(int id, PaymentStatus status, string currentUserId, string currentUserName)
        {
            var record = await _context.PayrollRecords.FindAsync(id);
            if (record == null)
                return ServiceResult.Fail("Payroll record not found.");

            record.Status = status;
            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, currentUserName, "Update", "PayrollRecord",
                id.ToString(), description: $"Updated payroll status to: {status}");

            return ServiceResult.Ok();
        }

        public async Task<List<SelectListItem>> GetStudentSelectListAsync()
        {
            return await _context.Students.Where(s => s.IsActive)
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.FirstName + " " + s.LastName + " (" + s.SAIDNumber + ")" })
                .ToListAsync();
        }

        public async Task<List<string>> GetPayPeriodsAsync()
        {
            return await _context.PayrollRecords
                .Where(p => p.PayPeriod != null)
                .Select(p => p.PayPeriod!)
                .Distinct().OrderByDescending(p => p).ToListAsync();
        }
    }
}
