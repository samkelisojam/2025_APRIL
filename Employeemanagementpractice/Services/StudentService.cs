using Employeemanagementpractice.Data;
using Employeemanagementpractice.Models;
using Employeemanagementpractice.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Employeemanagementpractice.Services
{
    public interface IStudentService
    {
        Task<(List<Student> Items, int TotalCount, int TotalPages)> SearchStudentsAsync(string? search, int? teamLeaderId, int page, int pageSize);
        Task<Student?> GetStudentProfileAsync(int id);
        Task<Student?> GetStudentByIdAsync(int id);
        Task<ServiceResult<int>> CreateStudentAsync(StudentViewModel model, string currentUserId, string currentUserName);
        Task<StudentViewModel?> GetStudentForEditAsync(int id);
        Task<ServiceResult> UpdateStudentAsync(StudentViewModel model, string currentUserId, string currentUserName);
        Task<ServiceResult> DeleteStudentAsync(int id, string currentUserId, string currentUserName);
        Task<byte[]?> GetProfileImageAsync(int id);
        Task<object> ValidateIdNumberAsync(string idNumber);
        Task<List<SelectListItem>> GetTeamLeaderSelectListAsync();
    }

    public class StudentService : IStudentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;
        private readonly ISaIdValidationService _saIdService;
        private readonly IFileStorageService _fileStorage;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public StudentService(ApplicationDbContext context, IAuditService audit,
            ISaIdValidationService saIdService, IFileStorageService fileStorage,
            UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _audit = audit;
            _saIdService = saIdService;
            _fileStorage = fileStorage;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<(List<Student> Items, int TotalCount, int TotalPages)> SearchStudentsAsync(
            string? search, int? teamLeaderId, int page, int pageSize)
        {
            var query = _context.Students.Include(s => s.TeamLeader).ThenInclude(t => t.User).Where(s => s.IsActive);

            if (teamLeaderId.HasValue)
                query = query.Where(s => s.TeamLeaderId == teamLeaderId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(st => st.FirstName.ToLower().Contains(s) ||
                    st.LastName.ToLower().Contains(s) ||
                    st.SAIDNumber.Contains(s) ||
                    (st.Email != null && st.Email.ToLower().Contains(s)));
            }

            var count = await query.CountAsync();
            var items = await query.OrderBy(s => s.LastName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var totalPages = (int)Math.Ceiling(count / (double)pageSize);

            return (items, count, totalPages);
        }

        public async Task<Student?> GetStudentProfileAsync(int id)
        {
            return await _context.Students
                .Include(s => s.TeamLeader).ThenInclude(t => t.User)
                .Include(s => s.Documents).Include(s => s.PayrollRecords)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<Student?> GetStudentByIdAsync(int id)
        {
            return await _context.Students.FindAsync(id);
        }

        public async Task<ServiceResult<int>> CreateStudentAsync(StudentViewModel model, string currentUserId, string currentUserName)
        {
            // Validate SA ID
            var (isValid, errorMsg, dob, gender) = _saIdService.ValidateSAID(model.SAIDNumber);
            if (!isValid)
                return ServiceResult<int>.Fail(errorMsg!, "SAIDNumber");

            // Check duplicate SA ID
            if (await _context.Students.AnyAsync(s => s.SAIDNumber == model.SAIDNumber))
                return ServiceResult<int>.Fail("A student with this SA ID already exists.", "SAIDNumber");

            // Check team leader capacity
            var tl = await _context.TeamLeaders
                .Include(t => t.Students.Where(s => s.IsActive))
                .FirstOrDefaultAsync(t => t.Id == model.TeamLeaderId);
            if (tl != null && tl.Students.Count >= tl.MaxStudents)
                return ServiceResult<int>.Fail($"Team leader has reached maximum capacity of {tl.MaxStudents} students.", "TeamLeaderId");

            var student = MapViewModelToStudent(model);
            student.DateOfBirth = dob;
            student.Gender = gender == "Male" ? Gender.Male : Gender.Female;

            // Profile image
            if (model.ProfileImage != null)
            {
                var (data, _, _) = await _fileStorage.SaveFileAsync(model.ProfileImage, "profiles");
                student.ProfileImageData = data;
            }

            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            // Save documents
            await SaveDocumentIfPresent(model.IDCopyFile, student.Id, DocumentType.IDCopy, true, currentUserName);
            await SaveDocumentIfPresent(model.QualificationFile, student.Id, DocumentType.Qualification, true, currentUserName);
            await SaveDocumentIfPresent(model.BankStatementFile, student.Id, DocumentType.BankStatement, true, currentUserName);
            await SaveDocumentIfPresent(model.Other1File, student.Id, DocumentType.Other1, false, currentUserName);
            await SaveDocumentIfPresent(model.Other2File, student.Id, DocumentType.Other2, false, currentUserName);
            await SaveDocumentIfPresent(model.Other3File, student.Id, DocumentType.Other3, false, currentUserName);
            await SaveDocumentIfPresent(model.Other4File, student.Id, DocumentType.Other4, false, currentUserName);

            await _audit.LogAsync(currentUserId, currentUserName, "Create", "Student",
                student.Id.ToString(), newValues: new { model.FirstName, model.LastName, model.SAIDNumber },
                description: $"Registered student: {model.FirstName} {model.LastName}");

            // ── Auto-create login account for the student ──
            if (!string.IsNullOrEmpty(model.Email))
            {
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser == null)
                {
                    var newUser = new ApplicationUser
                    {
                        UserName = model.Email,
                        Email = model.Email,
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        EmailConfirmed = true,
                        IsActive = true,
                        MustChangePassword = true,
                        PasswordChangeDeadline = DateTime.UtcNow.AddDays(3),
                        SecurityPin = "202612345678",
                        CreatedAt = DateTime.UtcNow
                    };
                    var createResult = await _userManager.CreateAsync(newUser, "Student@1234");
                    if (createResult.Succeeded)
                    {
                        if (!await _roleManager.RoleExistsAsync("Student"))
                            await _roleManager.CreateAsync(new IdentityRole("Student"));
                        await _userManager.AddToRoleAsync(newUser, "Student");
                        student.UserId = newUser.Id;
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    // Link existing user
                    student.UserId = existingUser.Id;
                    await _context.SaveChangesAsync();
                }
            }

            return ServiceResult<int>.Ok(student.Id);
        }

        public async Task<StudentViewModel?> GetStudentForEditAsync(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null) return null;

            return new StudentViewModel
            {
                Id = student.Id,
                TeamLeaderId = student.TeamLeaderId,
                FirstName = student.FirstName,
                MiddleName = student.MiddleName,
                LastName = student.LastName,
                PreferredName = student.PreferredName,
                Title = student.Title,
                SAIDNumber = student.SAIDNumber,
                Gender = student.Gender,
                Race = student.Race,
                Nationality = student.Nationality,
                HomeLanguage = student.HomeLanguage,
                MaritalStatus = student.MaritalStatus,
                DisabilityStatus = student.DisabilityStatus,
                DisabilityDescription = student.DisabilityDescription,
                Email = student.Email,
                Phone = student.Phone,
                AlternativePhone = student.AlternativePhone,
                WhatsAppNumber = student.WhatsAppNumber,
                StreetAddress = student.StreetAddress,
                Suburb = student.Suburb,
                City = student.City,
                Province = student.Province,
                PostalCode = student.PostalCode,
                PostalAddress = student.PostalAddress,
                PostalCity = student.PostalCity,
                PostalProvince = student.PostalProvince,
                PostalPostalCode = student.PostalPostalCode,
                SameAsPhysical = student.SameAsPhysical,
                QualificationType = student.QualificationType,
                QualificationName = student.QualificationName,
                Institution = student.Institution,
                YearCompleted = student.YearCompleted,
                StudentNumber = student.StudentNumber,
                HighestGradePass = student.HighestGradePass,
                OtherQualifications = student.OtherQualifications,
                PreviousEmployer = student.PreviousEmployer,
                PreviousJobTitle = student.PreviousJobTitle,
                YearsExperience = student.YearsExperience,
                WorkExperienceDescription = student.WorkExperienceDescription,
                Skills = student.Skills,
                DriversLicense = student.DriversLicense,
                HasOwnTransport = student.HasOwnTransport,
                NextOfKinName = student.NextOfKinName,
                NextOfKinRelationship = student.NextOfKinRelationship,
                NextOfKinPhone = student.NextOfKinPhone,
                NextOfKinAlternativePhone = student.NextOfKinAlternativePhone,
                NextOfKinEmail = student.NextOfKinEmail,
                NextOfKinAddress = student.NextOfKinAddress,
                BankName = student.BankName,
                BankAccountNumber = student.BankAccountNumber,
                BankBranchCode = student.BankBranchCode,
                BranchName = student.BranchName,
                AccountType = student.AccountType,
                AccountHolderName = student.AccountHolderName,
                Notes = student.Notes
            };
        }

        public async Task<ServiceResult> UpdateStudentAsync(StudentViewModel model, string currentUserId, string currentUserName)
        {
            var student = await _context.Students.FindAsync(model.Id);
            if (student == null)
                return ServiceResult.Fail("Student not found.");

            MapViewModelToExistingStudent(model, student);
            student.UpdatedAt = DateTime.UtcNow;

            if (model.ProfileImage != null)
            {
                var (data, _, _) = await _fileStorage.SaveFileAsync(model.ProfileImage, "profiles");
                student.ProfileImageData = data;
            }

            await _context.SaveChangesAsync();

            // Save new documents if provided
            await SaveDocumentIfPresent(model.IDCopyFile, student.Id, DocumentType.IDCopy, true, currentUserName);
            await SaveDocumentIfPresent(model.QualificationFile, student.Id, DocumentType.Qualification, true, currentUserName);
            await SaveDocumentIfPresent(model.BankStatementFile, student.Id, DocumentType.BankStatement, true, currentUserName);
            await SaveDocumentIfPresent(model.Other1File, student.Id, DocumentType.Other1, false, currentUserName);
            await SaveDocumentIfPresent(model.Other2File, student.Id, DocumentType.Other2, false, currentUserName);
            await SaveDocumentIfPresent(model.Other3File, student.Id, DocumentType.Other3, false, currentUserName);
            await SaveDocumentIfPresent(model.Other4File, student.Id, DocumentType.Other4, false, currentUserName);

            await _audit.LogAsync(currentUserId, currentUserName, "Update", "Student",
                student.Id.ToString(), description: $"Updated student: {model.FirstName} {model.LastName}");

            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> DeleteStudentAsync(int id, string currentUserId, string currentUserName)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null)
                return ServiceResult.Fail("Student not found.");

            student.IsActive = false;
            student.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _audit.LogAsync(currentUserId, currentUserName, "Delete", "Student",
                id.ToString(), description: $"Deactivated student: {student.FullName}");

            return ServiceResult.Ok();
        }

        public async Task<byte[]?> GetProfileImageAsync(int id)
        {
            var student = await _context.Students.FindAsync(id);
            return student?.ProfileImageData;
        }

        public async Task<object> ValidateIdNumberAsync(string idNumber)
        {
            var (isValid, errorMsg, dob, gender) = _saIdService.ValidateSAID(idNumber);
            return new { isValid, errorMsg, dateOfBirth = dob?.ToString("yyyy-MM-dd"), gender };
        }

        public async Task<List<SelectListItem>> GetTeamLeaderSelectListAsync()
        {
            return await _context.TeamLeaders.Include(t => t.User).Where(t => t.IsActive)
                .Select(t => new SelectListItem { Value = t.Id.ToString(), Text = t.User.FirstName + " " + t.User.LastName })
                .ToListAsync();
        }

        private Student MapViewModelToStudent(StudentViewModel model)
        {
            return new Student
            {
                TeamLeaderId = model.TeamLeaderId,
                FirstName = model.FirstName,
                MiddleName = model.MiddleName,
                LastName = model.LastName,
                PreferredName = model.PreferredName,
                Title = model.Title,
                SAIDNumber = model.SAIDNumber,
                Race = model.Race,
                Nationality = model.Nationality,
                HomeLanguage = model.HomeLanguage,
                MaritalStatus = model.MaritalStatus,
                DisabilityStatus = model.DisabilityStatus,
                DisabilityDescription = model.DisabilityDescription,
                Email = model.Email,
                Phone = model.Phone,
                AlternativePhone = model.AlternativePhone,
                WhatsAppNumber = model.WhatsAppNumber,
                StreetAddress = model.StreetAddress,
                Suburb = model.Suburb,
                City = model.City,
                Province = model.Province,
                PostalCode = model.PostalCode,
                PostalAddress = model.PostalAddress,
                PostalCity = model.PostalCity,
                PostalProvince = model.PostalProvince,
                PostalPostalCode = model.PostalPostalCode,
                SameAsPhysical = model.SameAsPhysical,
                QualificationType = model.QualificationType,
                QualificationName = model.QualificationName,
                Institution = model.Institution,
                YearCompleted = model.YearCompleted,
                StudentNumber = model.StudentNumber,
                HighestGradePass = model.HighestGradePass,
                OtherQualifications = model.OtherQualifications,
                PreviousEmployer = model.PreviousEmployer,
                PreviousJobTitle = model.PreviousJobTitle,
                YearsExperience = model.YearsExperience,
                WorkExperienceDescription = model.WorkExperienceDescription,
                Skills = model.Skills,
                DriversLicense = model.DriversLicense,
                HasOwnTransport = model.HasOwnTransport,
                NextOfKinName = model.NextOfKinName,
                NextOfKinRelationship = model.NextOfKinRelationship,
                NextOfKinPhone = model.NextOfKinPhone,
                NextOfKinAlternativePhone = model.NextOfKinAlternativePhone,
                NextOfKinEmail = model.NextOfKinEmail,
                NextOfKinAddress = model.NextOfKinAddress,
                BankName = model.BankName,
                BankAccountNumber = model.BankAccountNumber,
                BankBranchCode = model.BankBranchCode,
                BranchName = model.BranchName,
                AccountType = model.AccountType,
                AccountHolderName = model.AccountHolderName,
                Notes = model.Notes
            };
        }

        private void MapViewModelToExistingStudent(StudentViewModel model, Student student)
        {
            student.TeamLeaderId = model.TeamLeaderId;
            student.FirstName = model.FirstName;
            student.MiddleName = model.MiddleName;
            student.LastName = model.LastName;
            student.PreferredName = model.PreferredName;
            student.Title = model.Title;
            student.Race = model.Race;
            student.Nationality = model.Nationality;
            student.HomeLanguage = model.HomeLanguage;
            student.MaritalStatus = model.MaritalStatus;
            student.DisabilityStatus = model.DisabilityStatus;
            student.DisabilityDescription = model.DisabilityDescription;
            student.Email = model.Email;
            student.Phone = model.Phone;
            student.AlternativePhone = model.AlternativePhone;
            student.WhatsAppNumber = model.WhatsAppNumber;
            student.StreetAddress = model.StreetAddress;
            student.Suburb = model.Suburb;
            student.City = model.City;
            student.Province = model.Province;
            student.PostalCode = model.PostalCode;
            student.PostalAddress = model.PostalAddress;
            student.PostalCity = model.PostalCity;
            student.PostalProvince = model.PostalProvince;
            student.PostalPostalCode = model.PostalPostalCode;
            student.SameAsPhysical = model.SameAsPhysical;
            student.QualificationType = model.QualificationType;
            student.QualificationName = model.QualificationName;
            student.Institution = model.Institution;
            student.YearCompleted = model.YearCompleted;
            student.StudentNumber = model.StudentNumber;
            student.HighestGradePass = model.HighestGradePass;
            student.OtherQualifications = model.OtherQualifications;
            student.PreviousEmployer = model.PreviousEmployer;
            student.PreviousJobTitle = model.PreviousJobTitle;
            student.YearsExperience = model.YearsExperience;
            student.WorkExperienceDescription = model.WorkExperienceDescription;
            student.Skills = model.Skills;
            student.DriversLicense = model.DriversLicense;
            student.HasOwnTransport = model.HasOwnTransport;
            student.NextOfKinName = model.NextOfKinName;
            student.NextOfKinRelationship = model.NextOfKinRelationship;
            student.NextOfKinPhone = model.NextOfKinPhone;
            student.NextOfKinAlternativePhone = model.NextOfKinAlternativePhone;
            student.NextOfKinEmail = model.NextOfKinEmail;
            student.NextOfKinAddress = model.NextOfKinAddress;
            student.BankName = model.BankName;
            student.BankAccountNumber = model.BankAccountNumber;
            student.BankBranchCode = model.BankBranchCode;
            student.BranchName = model.BranchName;
            student.AccountType = model.AccountType;
            student.AccountHolderName = model.AccountHolderName;
            student.Notes = model.Notes;
        }

        private async Task SaveDocumentIfPresent(IFormFile? file, int studentId, DocumentType docType, bool mandatory, string uploadedBy)
        {
            if (file == null) return;

            var (data, ftpPath, storageType) = await _fileStorage.SaveFileAsync(file, $"documents/{studentId}");

            var doc = new StudentDocument
            {
                StudentId = studentId,
                DocumentType = docType,
                FileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}",
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                StorageType = storageType,
                FileData = storageType == StorageType.Database ? data : null,
                FtpPath = ftpPath,
                IsMandatory = mandatory,
                UploadedBy = uploadedBy
            };

            _context.StudentDocuments.Add(doc);
            await _context.SaveChangesAsync();
        }
    }
}
