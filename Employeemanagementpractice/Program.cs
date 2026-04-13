using Employeemanagementpractice.Data;
using Employeemanagementpractice.Hubs;
using Employeemanagementpractice.Models;
using Employeemanagementpractice.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using TaskStatusEnum = Employeemanagementpractice.Models.TaskStatus;

var builder = WebApplication.CreateBuilder(args);

// Database - SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Services
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<ISaIdValidationService, SaIdValidationService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ITeamLeaderService, TeamLeaderService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IPayrollService, PayrollService>();
builder.Services.AddScoped<IAnnouncementService, AnnouncementService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IFileManagementService, FileManagementService>();
builder.Services.AddScoped<ITrainingService, TrainingService>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddScoped<IGlobalSearchService, GlobalSearchService>();
builder.Services.AddScoped<ICalendarService, CalendarService>();
builder.Services.AddScoped<IBatchImportService, BatchImportService>();
builder.Services.AddScoped<ISystemHealthService, SystemHealthService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IStudentCardService, StudentCardService>();
builder.Services.AddScoped<IUserActivityService, UserActivityService>();
builder.Services.AddHttpContextAccessor();

// SignalR
builder.Services.AddSignalR();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Auto-migrate and seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // ── Roles ──
    string[] roles = { "Admin", "Manager", "TeamLeader", "Staff", "ReadOnly" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    // ── Helper to create user ──
    async Task<ApplicationUser> EnsureUser(string email, string first, string last, string password, string role, string? pin = null)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = first,
                LastName = last,
                EmailConfirmed = true,
                IsActive = true,
                SecurityPin = pin ?? "202612345678",
                CreatedAt = DateTime.UtcNow
            };
            var res = await userManager.CreateAsync(user, password);
            if (res.Succeeded)
                await userManager.AddToRoleAsync(user, role);
        }
        return user;
    }

    // ── 1. ADMIN Users ──
    var admin = await EnsureUser("admin@system.co.za", "System", "Administrator", "Admin@1234", "Admin");
    var admin2 = await EnsureUser("thabo.admin@system.co.za", "Thabo", "Molefe", "Admin@1234", "Admin");

    // ── 2. MANAGER Users ──
    var manager1 = await EnsureUser("sipho.manager@system.co.za", "Sipho", "Ndlovu", "Manager@1234", "Manager");
    var manager2 = await EnsureUser("zanele.manager@system.co.za", "Zanele", "Dlamini", "Manager@1234", "Manager");

    // ── 3. TEAM LEADER Users ──
    var tl1User = await EnsureUser("nomsa.tl@system.co.za", "Nomsa", "Khumalo", "Leader@1234", "TeamLeader");
    var tl2User = await EnsureUser("bongani.tl@system.co.za", "Bongani", "Mthembu", "Leader@1234", "TeamLeader");
    var tl3User = await EnsureUser("lindiwe.tl@system.co.za", "Lindiwe", "Zulu", "Leader@1234", "TeamLeader");

    // ── 4. STAFF Users ──
    var staff1 = await EnsureUser("kabelo.staff@system.co.za", "Kabelo", "Mokoena", "Staff@1234", "Staff");
    var staff2 = await EnsureUser("palesa.staff@system.co.za", "Palesa", "Mahlangu", "Staff@1234", "Staff");
    var staff3 = await EnsureUser("tshepo.staff@system.co.za", "Tshepo", "Sithole", "Staff@1234", "Staff");

    // ── 5. READONLY Users ──
    var ro1 = await EnsureUser("auditor@system.co.za", "External", "Auditor", "ReadOnly@1234", "ReadOnly");
    var ro2 = await EnsureUser("viewer@system.co.za", "Report", "Viewer", "ReadOnly@1234", "ReadOnly");

    // ── 6. TEAM LEADERS (entity) ──
    if (!db.TeamLeaders.Any())
    {
        var tl1 = new TeamLeader { UserId = tl1User.Id, EmployeeNumber = "TL-2025-001", Department = "Youth Development", MaxStudents = 30, DateJoined = new DateTime(2025, 1, 15) };
        var tl2 = new TeamLeader { UserId = tl2User.Id, EmployeeNumber = "TL-2025-002", Department = "Skills Training", MaxStudents = 25, DateJoined = new DateTime(2025, 2, 1) };
        var tl3 = new TeamLeader { UserId = tl3User.Id, EmployeeNumber = "TL-2025-003", Department = "Community Service", MaxStudents = 20, DateJoined = new DateTime(2025, 3, 10) };
        db.TeamLeaders.AddRange(tl1, tl2, tl3);
        await db.SaveChangesAsync();

        // ── 7. STUDENTS ── (10 students spread across 3 team leaders)
        var students = new List<Student>
        {
            new Student
            {
                TeamLeaderId = tl1.Id, FirstName = "Andile", LastName = "Nkosi", SAIDNumber = "9801015800086",
                Gender = Gender.Male, Race = Race.Black, Nationality = "South African", HomeLanguage = "Zulu",
                DateOfBirth = new DateTime(1998, 1, 1), Email = "andile.nkosi@email.co.za", Phone = "0712345001",
                StreetAddress = "45 Nelson Mandela Dr", Suburb = "Sandton", City = "Johannesburg", Province = Province.Gauteng, PostalCode = "2196",
                QualificationType = "Diploma", QualificationName = "Business Administration", Institution = "Wits University", YearCompleted = 2021,
                BankName = "FNB", BankAccountNumber = "62845001234", BankBranchCode = "250655", AccountType = "Savings", AccountHolderName = "Andile Nkosi",
                NextOfKinName = "Themba Nkosi", NextOfKinRelationship = "Father", NextOfKinPhone = "0823456001",
                MaritalStatus = MaritalStatus.Single, DisabilityStatus = DisabilityStatus.None,
                Skills = "Microsoft Office, Data Entry, Communication", HasOwnTransport = true, DriversLicense = "Code B",
                Notes = "Excellent performer with strong leadership potential"
            },
            new Student
            {
                TeamLeaderId = tl1.Id, FirstName = "Thandiwe", LastName = "Mabaso", SAIDNumber = "9905155100087",
                Gender = Gender.Female, Race = Race.Black, Nationality = "South African", HomeLanguage = "Sotho",
                DateOfBirth = new DateTime(1999, 5, 15), Email = "thandiwe.mabaso@email.co.za", Phone = "0712345002",
                StreetAddress = "12 Church St", Suburb = "Pretoria CBD", City = "Pretoria", Province = Province.Gauteng, PostalCode = "0001",
                QualificationType = "Certificate", QualificationName = "Project Management", Institution = "Unisa", YearCompleted = 2022,
                BankName = "Capitec", BankAccountNumber = "1234567002", BankBranchCode = "470010", AccountType = "Savings", AccountHolderName = "Thandiwe Mabaso",
                NextOfKinName = "Grace Mabaso", NextOfKinRelationship = "Mother", NextOfKinPhone = "0823456002",
                MaritalStatus = MaritalStatus.Single, DisabilityStatus = DisabilityStatus.None,
                Skills = "Event Planning, Administration, Writing", HasOwnTransport = false
            },
            new Student
            {
                TeamLeaderId = tl1.Id, FirstName = "Kagiso", LastName = "Modise", SAIDNumber = "9703205800088",
                Gender = Gender.Male, Race = Race.Black, Nationality = "South African", HomeLanguage = "Tswana",
                DateOfBirth = new DateTime(1997, 3, 20), Email = "kagiso.modise@email.co.za", Phone = "0712345003",
                StreetAddress = "78 Main Rd", Suburb = "Rustenburg", City = "Rustenburg", Province = Province.NorthWest, PostalCode = "0300",
                QualificationType = "Diploma", QualificationName = "IT Support", Institution = "Tshwane University", YearCompleted = 2020,
                BankName = "Standard Bank", BankAccountNumber = "001234503", BankBranchCode = "051001", AccountType = "Current", AccountHolderName = "Kagiso Modise",
                NextOfKinName = "David Modise", NextOfKinRelationship = "Brother", NextOfKinPhone = "0823456003",
                MaritalStatus = MaritalStatus.Single, DisabilityStatus = DisabilityStatus.None,
                Skills = "Networking, PC Repair, Troubleshooting", HasOwnTransport = true, DriversLicense = "Code B",
                PreviousEmployer = "CompuTech Solutions", PreviousJobTitle = "IT Intern", YearsExperience = 1
            },
            new Student
            {
                TeamLeaderId = tl2.Id, FirstName = "Lerato", LastName = "Phiri", SAIDNumber = "0001015100089",
                Gender = Gender.Female, Race = Race.Black, Nationality = "South African", HomeLanguage = "Xhosa",
                DateOfBirth = new DateTime(2000, 1, 1), Email = "lerato.phiri@email.co.za", Phone = "0712345004",
                StreetAddress = "23 Beach Rd", Suburb = "Muizenberg", City = "Cape Town", Province = Province.WesternCape, PostalCode = "7945",
                QualificationType = "Degree", QualificationName = "Social Work", Institution = "UCT", YearCompleted = 2023,
                BankName = "Nedbank", BankAccountNumber = "1098765004", BankBranchCode = "198765", AccountType = "Savings", AccountHolderName = "Lerato Phiri",
                NextOfKinName = "Noluthando Phiri", NextOfKinRelationship = "Mother", NextOfKinPhone = "0823456004",
                MaritalStatus = MaritalStatus.Single, DisabilityStatus = DisabilityStatus.None,
                Skills = "Counselling, Research, Report Writing", HasOwnTransport = false
            },
            new Student
            {
                TeamLeaderId = tl2.Id, FirstName = "Sizwe", LastName = "Gumede", SAIDNumber = "9806125800090",
                Gender = Gender.Male, Race = Race.Black, Nationality = "South African", HomeLanguage = "Zulu",
                DateOfBirth = new DateTime(1998, 6, 12), Email = "sizwe.gumede@email.co.za", Phone = "0712345005",
                StreetAddress = "56 Berea Rd", Suburb = "Berea", City = "Durban", Province = Province.KwaZuluNatal, PostalCode = "4001",
                QualificationType = "Certificate", QualificationName = "Accounting", Institution = "DUT", YearCompleted = 2021,
                BankName = "ABSA", BankAccountNumber = "4089765005", BankBranchCode = "632005", AccountType = "Current", AccountHolderName = "Sizwe Gumede",
                NextOfKinName = "Sipho Gumede", NextOfKinRelationship = "Father", NextOfKinPhone = "0823456005",
                MaritalStatus = MaritalStatus.Married, DisabilityStatus = DisabilityStatus.None,
                Skills = "Bookkeeping, Excel, SAP", HasOwnTransport = true, DriversLicense = "Code B",
                PreviousEmployer = "BDO Accountants", PreviousJobTitle = "Junior Accountant", YearsExperience = 2
            },
            new Student
            {
                TeamLeaderId = tl2.Id, FirstName = "Naledi", LastName = "Tladi", SAIDNumber = "0102205100091",
                Gender = Gender.Female, Race = Race.Black, Nationality = "South African", HomeLanguage = "Pedi",
                DateOfBirth = new DateTime(2001, 2, 20), Email = "naledi.tladi@email.co.za", Phone = "0712345006",
                StreetAddress = "89 Jorissen St", Suburb = "Polokwane", City = "Polokwane", Province = Province.Limpopo, PostalCode = "0700",
                QualificationType = "Diploma", QualificationName = "Marketing Management", Institution = "UL", YearCompleted = 2023,
                BankName = "FNB", BankAccountNumber = "62890006", BankBranchCode = "250655", AccountType = "Savings", AccountHolderName = "Naledi Tladi",
                NextOfKinName = "Peter Tladi", NextOfKinRelationship = "Father", NextOfKinPhone = "0823456006",
                MaritalStatus = MaritalStatus.Single, DisabilityStatus = DisabilityStatus.None,
                Skills = "Digital Marketing, Social Media, Content Creation", HasOwnTransport = false
            },
            new Student
            {
                TeamLeaderId = tl3.Id, FirstName = "Mandla", LastName = "Shabalala", SAIDNumber = "9709085800092",
                Gender = Gender.Male, Race = Race.Black, Nationality = "South African", HomeLanguage = "Swati",
                DateOfBirth = new DateTime(1997, 9, 8), Email = "mandla.shabalala@email.co.za", Phone = "0712345007",
                StreetAddress = "34 Government Blvd", Suburb = "Mbombela", City = "Nelspruit", Province = Province.Mpumalanga, PostalCode = "1200",
                QualificationType = "Degree", QualificationName = "Public Administration", Institution = "UP", YearCompleted = 2022,
                BankName = "Standard Bank", BankAccountNumber = "001234507", BankBranchCode = "051001", AccountType = "Savings", AccountHolderName = "Mandla Shabalala",
                NextOfKinName = "Nomvula Shabalala", NextOfKinRelationship = "Wife", NextOfKinPhone = "0823456007",
                MaritalStatus = MaritalStatus.Married, DisabilityStatus = DisabilityStatus.None,
                Skills = "Leadership, Public Speaking, Policy Analysis", HasOwnTransport = true, DriversLicense = "Code B",
                PreviousEmployer = "Mpumalanga Dept of Education", PreviousJobTitle = "Admin Officer", YearsExperience = 3
            },
            new Student
            {
                TeamLeaderId = tl3.Id, FirstName = "Ayanda", LastName = "Cele", SAIDNumber = "0004105100093",
                Gender = Gender.Female, Race = Race.Black, Nationality = "South African", HomeLanguage = "Zulu",
                DateOfBirth = new DateTime(2000, 4, 10), Email = "ayanda.cele@email.co.za", Phone = "0712345008",
                StreetAddress = "15 Long St", Suburb = "Bloemfontein", City = "Bloemfontein", Province = Province.FreeState, PostalCode = "9301",
                QualificationType = "Certificate", QualificationName = "Human Resources", Institution = "UFS", YearCompleted = 2023,
                BankName = "Capitec", BankAccountNumber = "1234567008", BankBranchCode = "470010", AccountType = "Savings", AccountHolderName = "Ayanda Cele",
                NextOfKinName = "Zandile Cele", NextOfKinRelationship = "Sister", NextOfKinPhone = "0823456008",
                MaritalStatus = MaritalStatus.Single, DisabilityStatus = DisabilityStatus.Physical, DisabilityDescription = "Mild mobility impairment",
                Skills = "HR Management, Recruitment, Employment Law", HasOwnTransport = false
            },
            new Student
            {
                TeamLeaderId = tl3.Id, FirstName = "Pieter", LastName = "van der Merwe", SAIDNumber = "9811225800094",
                Gender = Gender.Male, Race = Race.White, Nationality = "South African", HomeLanguage = "Afrikaans",
                DateOfBirth = new DateTime(1998, 11, 22), Email = "pieter.vdm@email.co.za", Phone = "0712345009",
                StreetAddress = "67 Voortrekker Rd", Suburb = "Bellville", City = "Cape Town", Province = Province.WesternCape, PostalCode = "7530",
                QualificationType = "Diploma", QualificationName = "Agriculture", Institution = "Stellenbosch University", YearCompleted = 2021,
                BankName = "ABSA", BankAccountNumber = "4089765009", BankBranchCode = "632005", AccountType = "Current", AccountHolderName = "Pieter van der Merwe",
                NextOfKinName = "Jan van der Merwe", NextOfKinRelationship = "Father", NextOfKinPhone = "0823456009",
                MaritalStatus = MaritalStatus.Single, DisabilityStatus = DisabilityStatus.None,
                Skills = "Farming, Project Management, Data Analysis", HasOwnTransport = true, DriversLicense = "Code EB"
            },
            new Student
            {
                TeamLeaderId = tl1.Id, FirstName = "Priya", LastName = "Naidoo", SAIDNumber = "0005315100095",
                Gender = Gender.Female, Race = Race.Indian, Nationality = "South African", HomeLanguage = "English",
                DateOfBirth = new DateTime(2000, 5, 31), Email = "priya.naidoo@email.co.za", Phone = "0712345010",
                StreetAddress = "102 Umhlanga Rocks Dr", Suburb = "Umhlanga", City = "Durban", Province = Province.KwaZuluNatal, PostalCode = "4320",
                QualificationType = "Degree", QualificationName = "Computer Science", Institution = "UKZN", YearCompleted = 2023,
                BankName = "Nedbank", BankAccountNumber = "1098765010", BankBranchCode = "198765", AccountType = "Savings", AccountHolderName = "Priya Naidoo",
                NextOfKinName = "Raj Naidoo", NextOfKinRelationship = "Father", NextOfKinPhone = "0823456010",
                MaritalStatus = MaritalStatus.Single, DisabilityStatus = DisabilityStatus.None,
                Skills = "Python, C#, SQL, Web Development", HasOwnTransport = false,
                Notes = "Top candidate for IT placement"
            }
        };
        db.Students.AddRange(students);
        await db.SaveChangesAsync();

        // ── 8. PAYROLL RECORDS ──
        var payrollRecords = new List<PayrollRecord>();
        var payPeriods = new[] { "2025-11", "2025-12", "2026-01", "2026-02", "2026-03" };
        var amounts = new[] { 3500m, 3500m, 4000m, 4000m, 4200m };
        foreach (var student in students)
        {
            for (int p = 0; p < payPeriods.Length; p++)
            {
                payrollRecords.Add(new PayrollRecord
                {
                    StudentId = student.Id,
                    PaymentDate = DateTime.Parse(payPeriods[p] + "-25"),
                    Amount = amounts[p],
                    PaymentMethod = p % 2 == 0 ? "Bank Transfer" : "EFT",
                    Reference = $"PAY-{payPeriods[p]}-{student.Id:D3}",
                    Status = p < 4 ? PaymentStatus.Paid : PaymentStatus.Pending,
                    PayPeriod = payPeriods[p],
                    CreatedBy = admin.Email,
                    CreatedAt = DateTime.Parse(payPeriods[p] + "-20")
                });
            }
        }
        db.PayrollRecords.AddRange(payrollRecords);
        await db.SaveChangesAsync();

        // ── 9. TASK ITEMS ──
        var tasks = new List<TaskItem>
        {
            new TaskItem { Title = "Complete Q1 student assessments", Description = "Review and submit all student performance assessments for Q1 2026", AssignedToUserId = tl1User.Id, CreatedByUserId = admin.Id, Priority = TaskPriority.High, Status = TaskStatusEnum.InProgress, DueDate = new DateTime(2026, 4, 30) },
            new TaskItem { Title = "Update student bank details", Description = "Verify and update bank details for all new students before payroll", AssignedToUserId = tl2User.Id, CreatedByUserId = manager1.Id, Priority = TaskPriority.Urgent, Status = TaskStatusEnum.New, DueDate = new DateTime(2026, 4, 20) },
            new TaskItem { Title = "Prepare monthly report for March", Description = "Compile monthly report including attendance, progress, and incidents", AssignedToUserId = staff1.Id, CreatedByUserId = manager1.Id, Priority = TaskPriority.Medium, Status = TaskStatusEnum.Completed, DueDate = new DateTime(2026, 4, 5) },
            new TaskItem { Title = "Organize career development workshop", Description = "Plan and execute career development workshop for all students in Gauteng", AssignedToUserId = tl3User.Id, CreatedByUserId = manager2.Id, Priority = TaskPriority.Medium, Status = TaskStatusEnum.New, DueDate = new DateTime(2026, 5, 15) },
            new TaskItem { Title = "Review student document compliance", Description = "Ensure all mandatory documents (ID copy, qualification, bank statement) are on file for every student", AssignedToUserId = staff2.Id, CreatedByUserId = admin.Id, Priority = TaskPriority.High, Status = TaskStatusEnum.InProgress, DueDate = new DateTime(2026, 4, 25) },
            new TaskItem { Title = "Conduct stipend audit for Q4 2025", Description = "Cross-reference payroll records with student attendance for Oct-Dec 2025", AssignedToUserId = staff3.Id, CreatedByUserId = manager2.Id, Priority = TaskPriority.Low, Status = TaskStatusEnum.OnHold, DueDate = new DateTime(2026, 5, 30) },
            new TaskItem { Title = "Onboard new students batch April 2026", Description = "Process applications and create profiles for the April 2026 intake", AssignedToUserId = tl1User.Id, CreatedByUserId = admin.Id, Priority = TaskPriority.Urgent, Status = TaskStatusEnum.InProgress, DueDate = new DateTime(2026, 4, 18) },
            new TaskItem { Title = "Submit quarterly report to NYDA", Description = "Prepare and submit the official quarterly report to the NYDA head office", AssignedToUserId = manager1.Id, CreatedByUserId = admin.Id, Priority = TaskPriority.High, Status = TaskStatusEnum.New, DueDate = new DateTime(2026, 4, 30) }
        };
        db.TaskItems.AddRange(tasks);
        await db.SaveChangesAsync();

        // ── 10. TASK COMMENTS ──
        db.TaskComments.AddRange(
            new TaskComment { TaskItemId = tasks[0].Id, UserId = tl1User.Id, UserName = tl1User.FullName, Comment = "Started reviewing assessments for Sandton students. 3 out of 4 complete.", CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new TaskComment { TaskItemId = tasks[0].Id, UserId = admin.Id, UserName = admin.FullName, Comment = "Please prioritize students whose stipend depends on this.", CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new TaskComment { TaskItemId = tasks[1].Id, UserId = tl2User.Id, UserName = tl2User.FullName, Comment = "Awaiting bank confirmation letters from 2 students.", CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new TaskComment { TaskItemId = tasks[2].Id, UserId = staff1.Id, UserName = staff1.FullName, Comment = "Report submitted and approved by manager.", CreatedAt = DateTime.UtcNow.AddDays(-5) },
            new TaskComment { TaskItemId = tasks[4].Id, UserId = staff2.Id, UserName = staff2.FullName, Comment = "Found 3 students missing ID copies. Requesting uploads.", CreatedAt = DateTime.UtcNow.AddHours(-6) }
        );
        await db.SaveChangesAsync();

        // ── 11. ANNOUNCEMENTS ──
        db.Announcements.AddRange(
            new Announcement { Title = "System Maintenance Scheduled", Message = "The EMS Portal will undergo scheduled maintenance on Saturday 19 April 2026 from 22:00 to 02:00. Please save all work before this time.", CreatedByUserId = admin.Id, TargetAudience = TargetAudience.All, CreatedAt = DateTime.UtcNow.AddDays(-3) },
            new Announcement { Title = "April Stipend Processing", Message = "April 2026 stipends will be processed on the 25th. Team leaders, please ensure all student records are up to date by the 20th.", CreatedByUserId = manager1.Id, TargetAudience = TargetAudience.TeamLeaders, CreatedAt = DateTime.UtcNow.AddDays(-5) },
            new Announcement { Title = "New Document Upload Policy", Message = "Effective immediately, all student documents must be uploaded within 48 hours of receipt. Documents older than 6 months must be re-verified.", CreatedByUserId = admin.Id, TargetAudience = TargetAudience.All, CreatedAt = DateTime.UtcNow.AddDays(-7) },
            new Announcement { Title = "Career Development Workshop - May 2026", Message = "A career development workshop will be held on 15 May 2026 at the Sandton Convention Centre. All students in Gauteng are expected to attend.", CreatedByUserId = manager2.Id, TargetAudience = TargetAudience.Students, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new Announcement { Title = "Quarterly Performance Review", Message = "All team leaders must complete Q1 2026 performance reviews for their students by 30 April. Use the Reports section to generate assessment summaries.", CreatedByUserId = admin.Id, TargetAudience = TargetAudience.TeamLeaders, CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        // ── 12. AUDIT LOGS ──
        var auditLogs = new List<AuditLog>
        {
            new AuditLog { UserId = admin.Id, UserName = admin.Email, Action = "Login", EntityType = "User", EntityId = admin.Id, Timestamp = DateTime.UtcNow.AddDays(-7) },
            new AuditLog { UserId = admin.Id, UserName = admin.Email, Action = "Create", EntityType = "Student", EntityId = "1", NewValues = "Created student Andile Nkosi", Timestamp = DateTime.UtcNow.AddDays(-6) },
            new AuditLog { UserId = tl1User.Id, UserName = tl1User.Email, Action = "Login", EntityType = "User", EntityId = tl1User.Id, Timestamp = DateTime.UtcNow.AddDays(-5) },
            new AuditLog { UserId = tl1User.Id, UserName = tl1User.Email, Action = "Update", EntityType = "Student", EntityId = "1", NewValues = "Updated contact info for Andile Nkosi", Timestamp = DateTime.UtcNow.AddDays(-5) },
            new AuditLog { UserId = manager1.Id, UserName = manager1.Email, Action = "Login", EntityType = "User", EntityId = manager1.Id, Timestamp = DateTime.UtcNow.AddDays(-4) },
            new AuditLog { UserId = manager1.Id, UserName = manager1.Email, Action = "Create", EntityType = "PayrollRecord", EntityId = "1", NewValues = "Created payroll for March 2026", Timestamp = DateTime.UtcNow.AddDays(-4) },
            new AuditLog { UserId = tl2User.Id, UserName = tl2User.Email, Action = "Login", EntityType = "User", EntityId = tl2User.Id, Timestamp = DateTime.UtcNow.AddDays(-3) },
            new AuditLog { UserId = tl2User.Id, UserName = tl2User.Email, Action = "Upload", EntityType = "Document", EntityId = "4", NewValues = "Uploaded ID copy for Lerato Phiri", Timestamp = DateTime.UtcNow.AddDays(-3) },
            new AuditLog { UserId = staff1.Id, UserName = staff1.Email, Action = "Export", EntityType = "Report", NewValues = "Exported student report to Excel", Timestamp = DateTime.UtcNow.AddDays(-2) },
            new AuditLog { UserId = admin.Id, UserName = admin.Email, Action = "Create", EntityType = "Announcement", EntityId = "1", NewValues = "Created system maintenance announcement", Timestamp = DateTime.UtcNow.AddDays(-3) },
            new AuditLog { UserId = tl3User.Id, UserName = tl3User.Email, Action = "Login", EntityType = "User", EntityId = tl3User.Id, Timestamp = DateTime.UtcNow.AddDays(-1) },
            new AuditLog { UserId = tl3User.Id, UserName = tl3User.Email, Action = "Update", EntityType = "Student", EntityId = "7", NewValues = "Updated qualification details for Mandla Shabalala", Timestamp = DateTime.UtcNow.AddDays(-1) },
            new AuditLog { UserId = admin.Id, UserName = admin.Email, Action = "Login", EntityType = "User", EntityId = admin.Id, Timestamp = DateTime.UtcNow },
            new AuditLog { UserId = admin.Id, UserName = admin.Email, Action = "Seed", EntityType = "System", NewValues = "Database seeded with sample data", Timestamp = DateTime.UtcNow }
        };
        db.AuditLogs.AddRange(auditLogs);
        await db.SaveChangesAsync();

        // ── 13. SAVED REPORTS ──
        db.SavedReports.AddRange(
            new SavedReport { ReportName = "All Students Full Profile", FieldsJson = "[\"FirstName\",\"LastName\",\"SAIDNumber\",\"Gender\",\"Email\",\"Phone\",\"City\",\"Province\",\"QualificationName\",\"BankName\"]", CreatedBy = admin.Email! },
            new SavedReport { ReportName = "Payroll Summary by Province", FieldsJson = "[\"FirstName\",\"LastName\",\"SAIDNumber\",\"Province\",\"BankName\",\"BankAccountNumber\",\"Amount\"]", CreatedBy = manager1.Email! },
            new SavedReport { ReportName = "Student Contact List", FieldsJson = "[\"FirstName\",\"LastName\",\"Email\",\"Phone\",\"WhatsAppNumber\",\"City\"]", CreatedBy = staff1.Email! }
        );
        await db.SaveChangesAsync();

        // ── 14. ROLE PERMISSIONS for Manager, TeamLeader, Staff, ReadOnly ──
        if (!db.RolePermissions.Any(rp => rp.RoleName == "Manager"))
        {
            var managerPerms = new List<RolePermission>();
            for (int i = 1; i <= 16; i++) // Managers: all except Admin permissions (17-19)
            {
                managerPerms.Add(new RolePermission { RoleName = "Manager", PermissionId = i, CanView = true, CanCreate = true, CanEdit = true, CanDelete = i <= 12 });
            }
            db.RolePermissions.AddRange(managerPerms);

            var tlPerms = new List<RolePermission>();
            foreach (var pid in new[] { 1, 3, 4, 5, 6, 7, 9, 11, 14, 15 }) // TeamLeader: Dashboard, view TLs, manage students/docs, view payroll/reports, manage/view tasks
            {
                tlPerms.Add(new RolePermission { RoleName = "TeamLeader", PermissionId = pid, CanView = true, CanCreate = pid == 4 || pid == 6 || pid == 14, CanEdit = pid == 4 || pid == 6, CanDelete = false });
            }
            db.RolePermissions.AddRange(tlPerms);

            var staffPerms = new List<RolePermission>();
            foreach (var pid in new[] { 1, 3, 5, 7, 9, 10, 11, 15 }) // Staff: Dashboard, view TLs/students/docs/payroll, export, view reports/tasks
            {
                staffPerms.Add(new RolePermission { RoleName = "Staff", PermissionId = pid, CanView = true, CanCreate = false, CanEdit = false, CanDelete = false });
            }
            db.RolePermissions.AddRange(staffPerms);

            var roPerms = new List<RolePermission>();
            foreach (var pid in new[] { 1, 3, 5, 7, 9, 11 }) // ReadOnly: Dashboard, view only TLs/students/docs/payroll/reports
            {
                roPerms.Add(new RolePermission { RoleName = "ReadOnly", PermissionId = pid, CanView = true, CanCreate = false, CanEdit = false, CanDelete = false });
            }
            db.RolePermissions.AddRange(roPerms);

            await db.SaveChangesAsync();
        }

        // ── Seed Training Courses ──
        if (!db.TrainingCourses.Any())
        {
            db.TrainingCourses.AddRange(
                new TrainingCourse { Title = "Employee Onboarding", Description = "Complete onboarding guide for new employees", Category = "Onboarding", Difficulty = "Beginner", DurationMinutes = 60, IsMandatory = true, IsActive = true, SortOrder = 1, Content = "<h3>Welcome to the Team!</h3><p>This course covers everything you need to know as a new employee including policies, systems, and your role responsibilities.</p><ul><li>Company overview and values</li><li>HR policies and procedures</li><li>System access and tools</li><li>Health and safety</li></ul>", CreatedBy = "admin@system.co.za", CreatedAt = DateTime.UtcNow },
                new TrainingCourse { Title = "Workplace Safety & Compliance", Description = "Safety protocols and regulatory compliance training", Category = "Safety", Difficulty = "Beginner", DurationMinutes = 45, IsMandatory = true, IsActive = true, SortOrder = 2, Content = "<h3>Safety First</h3><p>Learn about workplace safety protocols, emergency procedures, and compliance requirements.</p>", CreatedBy = "admin@system.co.za", CreatedAt = DateTime.UtcNow },
                new TrainingCourse { Title = "Leadership & Team Management", Description = "Develop leadership skills for team leaders and managers", Category = "Leadership", Difficulty = "Intermediate", DurationMinutes = 90, IsMandatory = false, IsActive = true, SortOrder = 3, Content = "<h3>Leading with Purpose</h3><p>This course covers essential leadership skills including delegation, motivation, conflict resolution, and performance management.</p>", CreatedBy = "admin@system.co.za", CreatedAt = DateTime.UtcNow },
                new TrainingCourse { Title = "Data Protection & Privacy (POPIA)", Description = "South African POPIA compliance training", Category = "Compliance", Difficulty = "Intermediate", DurationMinutes = 40, IsMandatory = true, IsActive = true, SortOrder = 4, Content = "<h3>Protecting Personal Information</h3><p>Understand your obligations under the Protection of Personal Information Act (POPIA) and how to handle personal data responsibly.</p>", CreatedBy = "admin@system.co.za", CreatedAt = DateTime.UtcNow },
                new TrainingCourse { Title = "Advanced Report Building", Description = "Master the report builder and data analysis tools", Category = "IT", Difficulty = "Advanced", DurationMinutes = 60, IsMandatory = false, IsActive = true, SortOrder = 5, Content = "<h3>Power Reporting</h3><p>Learn to create advanced reports, custom filters, Excel exports, and PDF generation using the system's report builder.</p>", CreatedBy = "admin@system.co.za", CreatedAt = DateTime.UtcNow },
                new TrainingCourse { Title = "Customer Service Excellence", Description = "Best practices for serving students and stakeholders", Category = "Soft Skills", Difficulty = "Beginner", DurationMinutes = 30, IsMandatory = false, IsActive = true, SortOrder = 6, Content = "<h3>Service with a Smile</h3><p>Improve your communication, empathy, and problem-solving skills when working with students and stakeholders.</p>", CreatedBy = "admin@system.co.za", CreatedAt = DateTime.UtcNow }
            );
            await db.SaveChangesAsync();
        }

        // ── Seed Calendar Events ──
        if (!db.CalendarEvents.Any())
        {
            var now = DateTime.UtcNow;
            db.CalendarEvents.AddRange(
                new CalendarEvent { Title = "Monthly Staff Meeting", Description = "All-hands monthly meeting", StartDate = now.AddDays(3), EndDate = now.AddDays(3).AddHours(2), Location = "Main Boardroom", EventType = "Meeting", Color = "#0d6efd", IsAllDay = false, CreatedByUserId = admin.Id, CreatedAt = now },
                new CalendarEvent { Title = "Payroll Processing Deadline", Description = "Submit all payroll data", StartDate = now.AddDays(10), EventType = "Deadline", Color = "#dc3545", IsAllDay = true, CreatedByUserId = admin.Id, CreatedAt = now },
                new CalendarEvent { Title = "Safety Training Workshop", Description = "Mandatory safety training for all staff", StartDate = now.AddDays(7), EndDate = now.AddDays(7).AddHours(3), Location = "Training Room A", EventType = "Training", Color = "#198754", IsAllDay = false, CreatedByUserId = admin.Id, CreatedAt = now },
                new CalendarEvent { Title = "Heritage Day", Description = "Public holiday", StartDate = new DateTime(now.Year, 9, 24), EventType = "Holiday", Color = "#6f42c1", IsAllDay = true, CreatedByUserId = admin.Id, CreatedAt = now },
                new CalendarEvent { Title = "Quarter Review Presentation", Description = "Quarterly performance review with management", StartDate = now.AddDays(14), EndDate = now.AddDays(14).AddHours(1), Location = "Conference Room", EventType = "Meeting", Color = "#fd7e14", IsAllDay = false, CreatedByUserId = admin.Id, CreatedAt = now }
            );
            await db.SaveChangesAsync();
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Track user activity (page views, downloads)
app.UseMiddleware<Employeemanagementpractice.Services.ActivityTrackingMiddleware>();

app.MapHub<NotificationHub>("/notificationHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
