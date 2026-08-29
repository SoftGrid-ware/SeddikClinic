using Microsoft.EntityFrameworkCore;
using SeddikClinic.Core.Entities.Appointments;
using SeddikClinic.Core.Entities.Billing;
using SeddikClinic.Core.Entities.Financial;
using SeddikClinic.Core.Entities.Identity;

namespace SeddikClinic.Infrastructure.Data;

public class SeddikClinicDbContext : DbContext
{
    public SeddikClinicDbContext(DbContextOptions<SeddikClinicDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<ClinicService> ClinicServices => Set<ClinicService>();

    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<RecurringExpense> RecurringExpenses => Set<RecurringExpense>();
    public DbSet<ExpenseAttachment> ExpenseAttachments => Set<ExpenseAttachment>();
    public DbSet<MonthlyBudget> MonthlyBudgets => Set<MonthlyBudget>();
    public DbSet<FinancialPeriod> FinancialPeriods => Set<FinancialPeriod>();
    public DbSet<FinancialPeriodClosing> FinancialPeriodClosings => Set<FinancialPeriodClosing>();
    public DbSet<FinancialAuditLog> FinancialAuditLogs => Set<FinancialAuditLog>();
    public DbSet<DailyShift> DailyShifts => Set<DailyShift>();
    
    public DbSet<PatientInvoice> PatientInvoices => Set<PatientInvoice>();
    public DbSet<PatientPayment> PatientPayments => Set<PatientPayment>();
    public DbSet<PatientRefund> PatientRefunds => Set<PatientRefund>();

    public DbSet<DentalToothRecord> DentalToothRecords => Set<DentalToothRecord>();
    public DbSet<PatientDentalImage> PatientDentalImages => Set<PatientDentalImage>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<DentalDrugCatalogItem> DentalDrugCatalogItems => Set<DentalDrugCatalogItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // إعدادات AppUser
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // إعدادات Patient
        modelBuilder.Entity<Patient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(30);
            entity.Property(e => e.PatientCode).HasMaxLength(50);
            entity.HasIndex(e => e.PhoneNumber);
            entity.HasIndex(e => e.FullName);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // إعدادات Appointment
        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AppointmentNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DoctorName).HasMaxLength(150);
            entity.Property(e => e.ServiceType).HasMaxLength(150);

            entity.HasIndex(e => e.AppointmentDate);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.PatientId);
            entity.HasIndex(e => e.DoctorId);

            entity.HasOne(e => e.Patient)
                .WithMany(p => p.Appointments)
                .HasForeignKey(e => e.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // إعدادات ExpenseCategory
        modelBuilder.Entity<ExpenseCategory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.NameAr).IsRequired().HasMaxLength(150);
            entity.Property(e => e.ColorHex).HasMaxLength(10);
            entity.Property(e => e.Icon).HasMaxLength(50);
        });

        // إعدادات Expense
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ExpenseNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(250);
            entity.Property(e => e.BeneficiaryName).HasMaxLength(200);
            entity.Property(e => e.ReceiptNumber).HasMaxLength(100);

            entity.HasIndex(e => e.ExpenseNumber).IsUnique();
            entity.HasIndex(e => e.PaymentDate);
            entity.HasIndex(e => e.CategoryId);
            entity.HasIndex(e => e.BranchId);
            entity.HasIndex(e => e.DoctorId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.IsDeleted);

            entity.HasOne(e => e.Category)
                .WithMany(c => c.Expenses)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // إعدادات RecurringExpense
        modelBuilder.Entity<RecurringExpense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(250);
            entity.Property(e => e.BeneficiaryName).HasMaxLength(200);
            entity.HasIndex(e => e.BranchId);
            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.Category)
                .WithMany(c => c.RecurringExpenses)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // إعدادات ExpenseAttachment
        modelBuilder.Entity<ExpenseAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(250);
            entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(250);
            entity.Property(e => e.FileUrl).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(1000);
            entity.Property(e => e.ContentType).HasMaxLength(100);

            entity.HasOne(e => e.Expense)
                .WithMany(ex => ex.Attachments)
                .HasForeignKey(e => e.ExpenseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // إعدادات MonthlyBudget
        modelBuilder.Entity<MonthlyBudget>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CategoryId, e.BranchId, e.Year, e.Month }).IsUnique();

            entity.HasOne(e => e.Category)
                .WithMany(c => c.MonthlyBudgets)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // إعدادات FinancialPeriod
        modelBuilder.Entity<FinancialPeriod>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.BranchId, e.Year, e.Month }).IsUnique();
        });

        // إعدادات FinancialPeriodClosing
        modelBuilder.Entity<FinancialPeriodClosing>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Period)
                .WithOne(p => p.ClosingDetails)
                .HasForeignKey<FinancialPeriodClosing>(e => e.PeriodId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // إعدادات DailyShift (تقفيل شيفت اليوم)
        modelBuilder.Entity<DailyShift>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ShiftNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.OpenedByUserName).HasMaxLength(150);
            entity.Property(e => e.ClosedByUserName).HasMaxLength(150);
            entity.Property(e => e.HandoverToUserName).HasMaxLength(150);
            entity.Property(e => e.DifferenceReason).HasMaxLength(500);
            entity.Property(e => e.HandoverNotes).HasMaxLength(1000);
            entity.HasIndex(e => e.ShiftDate);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.BranchId);
        });

        // إعدادات FinancialAuditLog
        modelBuilder.Entity<FinancialAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RecordId).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => new { e.EntityName, e.RecordId });
            entity.HasIndex(e => e.Timestamp);
        });

        // إعدادات الفواتير والمدفوعات
        modelBuilder.Entity<PatientInvoice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.InvoiceNumber).IsUnique();
            entity.HasIndex(e => e.InvoiceDate);
            entity.HasIndex(e => e.DoctorId);
            entity.HasIndex(e => e.BranchId);
        });

        modelBuilder.Entity<PatientPayment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReceiptNumber).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.PaymentDate);
            entity.HasIndex(e => e.DoctorId);
            entity.HasIndex(e => e.BranchId);

            entity.HasOne(e => e.Invoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PatientRefund>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RefundNumber).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.RefundDate);
        });

        SeedDefaultCategories(modelBuilder);
        SeedDefaultClinicServices(modelBuilder);
    }

    private static void SeedDefaultClinicServices(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClinicService>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.DefaultPrice).HasPrecision(18, 2);
        });

        var services = new List<ClinicService>
        {
            new() { Id = new Guid("22222222-2222-2222-2222-222222220001"), Name = "كشف واستشارة طبية", DefaultPrice = 250m, Category = "كشف وفحص", DisplayOrder = 1, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id = new Guid("22222222-2222-2222-2222-222222220002"), Name = "حشو أسنان كمبوزيت", DefaultPrice = 500m, Category = "علاج وتجميل", DisplayOrder = 2, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id = new Guid("22222222-2222-2222-2222-222222220003"), Name = "علاج جذور وعصب", DefaultPrice = 800m, Category = "علاج وتجميل", DisplayOrder = 3, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id = new Guid("22222222-2222-2222-2222-222222220004"), Name = "تنظيف وتلميع أسنان وتكلسات", DefaultPrice = 400m, Category = "وقاية وتجميل", DisplayOrder = 4, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id = new Guid("22222222-2222-2222-2222-222222220005"), Name = "تبييض أسنان احترافي", DefaultPrice = 1500m, Category = "وقاية وتجميل", DisplayOrder = 5, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id = new Guid("22222222-2222-2222-2222-222222220006"), Name = "تركيبات وتيجان زيركون", DefaultPrice = 2500m, Category = "تركيبات", DisplayOrder = 6, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id = new Guid("22222222-2222-2222-2222-222222220007"), Name = "زراعة أسنان", DefaultPrice = 5000m, Category = "جراحة وزراعة", DisplayOrder = 7, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id = new Guid("22222222-2222-2222-2222-222222220008"), Name = "تقويم أسنان", DefaultPrice = 10000m, Category = "تقويم", DisplayOrder = 8, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id = new Guid("22222222-2222-2222-2222-222222220009"), Name = "خلع ضرس وجراحة", DefaultPrice = 350m, Category = "جراحة وزراعة", DisplayOrder = 9, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        };

        modelBuilder.Entity<ClinicService>().HasData(services);
    }

    private static void SeedDefaultCategories(ModelBuilder modelBuilder)
    {
        var categories = new List<ExpenseCategory>
        {
            new() { Id = new Guid("11111111-1111-1111-1111-111111110001"), Name = "Clinic Rent", NameAr = "إيجار العيادة", Code = "RENT", Icon = "home", ColorHex = "#3B82F6", IsDirectCost = false, DisplayOrder = 1 },
            new() { Id = new Guid("11111111-1111-1111-1111-111111110002"), Name = "Staff Salaries", NameAr = "رواتب الموظفين", Code = "SALARY", Icon = "users", ColorHex = "#10B981", IsDirectCost = false, DisplayOrder = 2 },
            new() { Id = new Guid("11111111-1111-1111-1111-111111110003"), Name = "Electricity", NameAr = "الكهرباء", Code = "ELEC", Icon = "zap", ColorHex = "#F59E0B", IsDirectCost = false, DisplayOrder = 3 },
            new() { Id = new Guid("11111111-1111-1111-1111-111111110004"), Name = "Water", NameAr = "المياه", Code = "WATER", Icon = "droplet", ColorHex = "#06B6D4", IsDirectCost = false, DisplayOrder = 4 },
            new() { Id = new Guid("11111111-1111-1111-1111-111111110005"), Name = "Internet & Phone", NameAr = "الإنترنت والهاتف", Code = "COMM", Icon = "wifi", ColorHex = "#8B5CF6", IsDirectCost = false, DisplayOrder = 5 },
            new() { Id = new Guid("11111111-1111-1111-1111-111111110006"), Name = "Dental Supplies", NameAr = "مستلزمات الأسنان", Code = "SUPPLIES", Icon = "package", ColorHex = "#EC4899", IsDirectCost = true, DisplayOrder = 6 },
            new() { Id = new Guid("11111111-1111-1111-1111-111111110007"), Name = "Filling Materials", NameAr = "مواد الحشو", Code = "FILLING", Icon = "shield", ColorHex = "#6366F1", IsDirectCost = true, DisplayOrder = 7 },
            new() { Id = new Guid("11111111-1111-1111-1111-111111110008"), Name = "Sterilization Materials", NameAr = "مواد التعقيم", Code = "STERIL", Icon = "check-circle", ColorHex = "#14B8A6", IsDirectCost = true, DisplayOrder = 8 },
            new() { Id = new Guid("11111111-1111-1111-1111-111111110009"), Name = "Medical Tools", NameAr = "الأدوات الطبية", Code = "TOOLS", Icon = "tool", ColorHex = "#64748B", IsDirectCost = true, DisplayOrder = 9 },
            new() { Id = new Guid("11111111-1111-1111-1111-111111110010"), Name = "Equipment Maintenance", NameAr = "صيانة الأجهزة", Code = "MAINT", Icon = "settings", ColorHex = "#D97706", IsDirectCost = false, DisplayOrder = 10 },
            new() { Id = new Guid("11111111-1111-1111-1111-111111110011"), Name = "Lab & Prosthetics", NameAr = "المعمل والتركيبات", Code = "LAB", Icon = "activity", ColorHex = "#EF4444", IsDirectCost = true, DisplayOrder = 11 },
            new() { Id = new Guid("11111111-1111-1111-1111-111111110012"), Name = "X-Ray Supplies", NameAr = "الأشعة", Code = "XRAY", Icon = "camera", ColorHex = "#475569", IsDirectCost = true, DisplayOrder = 12 },
            new() { Id = new Guid("11111111-1111-1111-1111-111111110013"), Name = "Marketing & Ads", NameAr = "التسويق والإعلانات", Code = "MKT", Icon = "share-2", ColorHex = "#F43F5E", IsDirectCost = false, DisplayOrder = 13 },
            new() { Id = new Guid("11111111-1111-1111-1111-111111110014"), Name = "Cleaning & Hospitality", NameAr = "النظافة", Code = "CLEAN", Icon = "trash-2", ColorHex = "#A855F7", IsDirectCost = false, DisplayOrder = 14 },
            new() { Id = new Guid("11111111-1111-1111-1111-111111110015"), Name = "Taxes & Fees", NameAr = "الضرائب والرسوم", Code = "TAX", Icon = "file-text", ColorHex = "#78716C", IsDirectCost = false, DisplayOrder = 15 },
            new() { Id = new Guid("11111111-1111-1111-1111-111111110016"), Name = "Transportation & Delivery", NameAr = "النقل والتوصيل", Code = "TRANS", Icon = "truck", ColorHex = "#0EA5E9", IsDirectCost = false, DisplayOrder = 16 },
            new() { Id = new Guid("11111111-1111-1111-1111-111111110017"), Name = "Other Expenses", NameAr = "مصروفات أخرى", Code = "OTHER", Icon = "more-horizontal", ColorHex = "#94A3B8", IsDirectCost = false, DisplayOrder = 17 }
        };

        modelBuilder.Entity<ExpenseCategory>().HasData(categories);
    }
}
