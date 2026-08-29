using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SeddikClinic.Api.BackgroundServices;
using SeddikClinic.Api.Health;
using SeddikClinic.Core.Entities.Appointments;
using SeddikClinic.Core.Entities.Identity;
using SeddikClinic.Core.Enums;
using SeddikClinic.Core.Interfaces;
using SeddikClinic.Infrastructure.Data;
using SeddikClinic.Infrastructure.Services;

// تفعيل التوافق مع التواريخ لـ PostgreSQL
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// دعم المنافذ وسماح الاتصال من الشبكة المحلية وأجهزة المحمول
var cloudPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(cloudPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{cloudPort}");
}
else
{
    builder.WebHost.UseUrls("http://0.0.0.0:5000", "http://0.0.0.0:8080");
}

// 1. إعداد قاعدة البيانات (PostgreSQL سحابياً من Neon أو SQLite محلياً)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? builder.Configuration["DATABASE_URL"]
    ?? "Data Source=seddik_clinic_local.db";

var isSqlite = connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase) || 
               connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase);

if (!isSqlite)
{
    if (connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) || 
        connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':');
        connectionString = $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={(userInfo.Length > 1 ? userInfo[1] : "")};SSL Mode=Require;Trust Server Certificate=true";
    }

    builder.Services.AddDbContext<SeddikClinicDbContext>(options =>
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null);
        });
    });
}
else
{
    builder.Services.AddDbContext<SeddikClinicDbContext>(options =>
    {
        options.UseSqlite(connectionString);
    });
}

// 2. تسجيل الخدمات والطبقات المعمارية
builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();
builder.Services.AddScoped<IFileStorageService, CloudflareR2StorageService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IFinancialPeriodService, FinancialPeriodService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IFinancialReportService, FinancialReportService>();
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IClinicServiceCatalogService, ClinicServiceCatalogService>();
builder.Services.AddScoped<IDentalChartService, DentalChartService>();
builder.Services.AddScoped<IPrescriptionService, PrescriptionService>();
builder.Services.AddScoped<IClinicAnalyticsService, ClinicAnalyticsService>();
builder.Services.AddScoped<IDailyShiftService, DailyShiftService>();

// 3. خدمة المصروفات الشهرية التلقائية
builder.Services.AddHostedService<RecurringExpensesWorker>();

// 4. الفحص الصحي
builder.Services.AddHealthChecks()
    .AddCheck<CloudServicesHealthCheck>("CloudAndDatabaseHealth");

// 5. إعداد المصادقة والتفويض
var jwtSecret = builder.Configuration["JwtSettings:Secret"] ?? "A_VERY_LONG_SECRET_KEY_FOR_JWT_AUTHENTICATION_SEDDIC_CLINIC_2026_PRODUCTION";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
});
builder.Services.AddAuthorization();

// 6. إعداد CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 7. Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var app = builder.Build();

// إنشاء الجداول وبذر البيانات الافتراضية
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<SeddikClinicDbContext>();
        db.Database.EnsureCreated();

        if (db.Database.IsNpgsql())
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""AppUsers"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""Username"" varchar(100) NOT NULL,
                    ""PasswordHash"" text NOT NULL,
                    ""FullName"" varchar(200) NOT NULL,
                    ""PhoneNumber"" varchar(30),
                    ""Role"" int NOT NULL DEFAULT 2,
                    ""CanViewFinancials"" boolean NOT NULL DEFAULT false,
                    ""CanManageExpenses"" boolean NOT NULL DEFAULT true,
                    ""CanCancelExpenses"" boolean NOT NULL DEFAULT false,
                    ""CanManageAppointments"" boolean NOT NULL DEFAULT true,
                    ""CanManagePatients"" boolean NOT NULL DEFAULT true,
                    ""CanExportReports"" boolean NOT NULL DEFAULT false,
                    ""CanManageUsers"" boolean NOT NULL DEFAULT false,
                    ""IsActive"" boolean NOT NULL DEFAULT true,
                    ""LastLoginAt"" timestamp with time zone,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""IsDeleted"" boolean NOT NULL DEFAULT false
                );

                CREATE TABLE IF NOT EXISTS ""Patients"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""PatientCode"" varchar(50),
                    ""FullName"" varchar(200) NOT NULL,
                    ""PhoneNumber"" varchar(30) NOT NULL,
                    ""AlternativePhone"" varchar(30),
                    ""NationalId"" varchar(30),
                    ""Gender"" varchar(20),
                    ""BirthDate"" timestamp with time zone,
                    ""Age"" int,
                    ""Address"" text,
                    ""BloodGroup"" varchar(10),
                    ""MedicalHistory"" text,
                    ""Allergies"" text,
                    ""Notes"" text,
                    ""PasswordHash"" text,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""IsDeleted"" boolean NOT NULL DEFAULT false
                );

                ALTER TABLE ""Patients"" ADD COLUMN IF NOT EXISTS ""PasswordHash"" text;

                CREATE TABLE IF NOT EXISTS ""Appointments"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""AppointmentNumber"" varchar(50) NOT NULL,
                    ""PatientId"" uuid NOT NULL REFERENCES ""Patients""(""Id"") ON DELETE RESTRICT,
                    ""DoctorId"" uuid,
                    ""DoctorName"" varchar(150),
                    ""BranchId"" uuid,
                    ""AppointmentDate"" timestamp with time zone NOT NULL,
                    ""StartTime"" interval NOT NULL,
                    ""EndTime"" interval NOT NULL,
                    ""ServiceType"" varchar(150),
                    ""ReasonForVisit"" text,
                    ""Status"" int NOT NULL DEFAULT 1,
                    ""TotalFees"" numeric(18,2) NOT NULL DEFAULT 0,
                    ""DepositAmount"" numeric(18,2) NOT NULL DEFAULT 0,
                    ""IsDepositPaid"" boolean NOT NULL DEFAULT false,
                    ""Notes"" text,
                    ""CancellationReason"" text,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""CreatedByUserName"" varchar(100),
                    ""IsDeleted"" boolean NOT NULL DEFAULT false
                );

                CREATE TABLE IF NOT EXISTS ""ClinicServices"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""Name"" varchar(200) NOT NULL,
                    ""DefaultPrice"" numeric(18,2) NOT NULL DEFAULT 0,
                    ""Description"" text,
                    ""Category"" varchar(100),
                    ""IsActive"" boolean NOT NULL DEFAULT true,
                    ""DisplayOrder"" int NOT NULL DEFAULT 0,
                    ""CreatedAt"" timestamp with time zone NOT NULL
                );

                -- إضافة أي أعمدة جديدة تلقائياً إذا كانت الجداول منشأة مسبقاً
                ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""CancellationReason"" text;
                ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""TotalFees"" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""DepositAmount"" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""IsDepositPaid"" boolean NOT NULL DEFAULT false;
                ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""DoctorId"" uuid;
                ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""DoctorName"" varchar(150);
                ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""CreatedByUserName"" varchar(100);
                ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""Notes"" text;
                ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""ServiceType"" varchar(150);

                ALTER TABLE ""Patients"" ADD COLUMN IF NOT EXISTS ""PatientCode"" varchar(50);
                ALTER TABLE ""Patients"" ADD COLUMN IF NOT EXISTS ""AlternativePhone"" varchar(30);
                ALTER TABLE ""Patients"" ADD COLUMN IF NOT EXISTS ""NationalId"" varchar(30);
                ALTER TABLE ""Patients"" ADD COLUMN IF NOT EXISTS ""Gender"" varchar(20);
                ALTER TABLE ""Patients"" ADD COLUMN IF NOT EXISTS ""BirthDate"" timestamp with time zone;
                ALTER TABLE ""Patients"" ADD COLUMN IF NOT EXISTS ""Age"" int;
                ALTER TABLE ""Patients"" ADD COLUMN IF NOT EXISTS ""Address"" text;
                ALTER TABLE ""Patients"" ADD COLUMN IF NOT EXISTS ""BloodGroup"" varchar(10);
                ALTER TABLE ""Patients"" ADD COLUMN IF NOT EXISTS ""MedicalHistory"" text;
                ALTER TABLE ""Patients"" ADD COLUMN IF NOT EXISTS ""Allergies"" text;
                ALTER TABLE ""Patients"" ADD COLUMN IF NOT EXISTS ""Notes"" text;

                ALTER TABLE ""ClinicServices"" ADD COLUMN IF NOT EXISTS ""Category"" varchar(100);
                ALTER TABLE ""ClinicServices"" ADD COLUMN IF NOT EXISTS ""Description"" text;
                ALTER TABLE ""ClinicServices"" ADD COLUMN IF NOT EXISTS ""DefaultPrice"" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE ""ClinicServices"" ADD COLUMN IF NOT EXISTS ""IsActive"" boolean NOT NULL DEFAULT true;
                ALTER TABLE ""ClinicServices"" ADD COLUMN IF NOT EXISTS ""DisplayOrder"" int NOT NULL DEFAULT 0;

                ALTER TABLE ""AppUsers"" ADD COLUMN IF NOT EXISTS ""CanViewFinancials"" boolean NOT NULL DEFAULT false;
                ALTER TABLE ""AppUsers"" ADD COLUMN IF NOT EXISTS ""CanManageExpenses"" boolean NOT NULL DEFAULT true;
                ALTER TABLE ""AppUsers"" ADD COLUMN IF NOT EXISTS ""CanCancelExpenses"" boolean NOT NULL DEFAULT false;
                ALTER TABLE ""AppUsers"" ADD COLUMN IF NOT EXISTS ""CanManageAppointments"" boolean NOT NULL DEFAULT true;
                ALTER TABLE ""AppUsers"" ADD COLUMN IF NOT EXISTS ""CanManagePatients"" boolean NOT NULL DEFAULT true;
                ALTER TABLE ""AppUsers"" ADD COLUMN IF NOT EXISTS ""CanExportReports"" boolean NOT NULL DEFAULT false;
                ALTER TABLE ""AppUsers"" ADD COLUMN IF NOT EXISTS ""CanManageUsers"" boolean NOT NULL DEFAULT false;
                ALTER TABLE ""AppUsers"" ADD COLUMN IF NOT EXISTS ""PhoneNumber"" varchar(30);

                CREATE TABLE IF NOT EXISTS ""DentalToothRecords"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""PatientId"" uuid NOT NULL REFERENCES ""Patients""(""Id"") ON DELETE CASCADE,
                    ""ToothNumber"" int NOT NULL,
                    ""Condition"" int NOT NULL DEFAULT 1,
                    ""AffectedSurfaces"" varchar(100),
                    ""Notes"" text,
                    ""EstimatedCost"" numeric(18,2) NOT NULL DEFAULT 0,
                    ""IsCompleted"" boolean NOT NULL DEFAULT false,
                    ""UpdatedAt"" timestamp with time zone NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL
                );

                CREATE TABLE IF NOT EXISTS ""PatientDentalImages"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""PatientId"" uuid NOT NULL REFERENCES ""Patients""(""Id"") ON DELETE CASCADE,
                    ""Title"" varchar(200) NOT NULL,
                    ""ImageType"" int NOT NULL DEFAULT 7,
                    ""ImageUrl"" text NOT NULL,
                    ""ThumbnailUrl"" text,
                    ""Notes"" text,
                    ""AssociatedToothNumber"" int,
                    ""TakenAt"" timestamp with time zone NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL
                );

                CREATE TABLE IF NOT EXISTS ""Prescriptions"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""PrescriptionNumber"" varchar(50) NOT NULL,
                    ""PatientId"" uuid NOT NULL REFERENCES ""Patients""(""Id"") ON DELETE CASCADE,
                    ""AppointmentId"" uuid REFERENCES ""Appointments""(""Id"") ON DELETE SET NULL,
                    ""DoctorName"" varchar(150) NOT NULL,
                    ""Diagnosis"" text,
                    ""GeneralInstructions"" text,
                    ""IssuedAt"" timestamp with time zone NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""IsDeleted"" boolean NOT NULL DEFAULT false
                );

                CREATE TABLE IF NOT EXISTS ""PrescriptionItems"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""PrescriptionId"" uuid NOT NULL REFERENCES ""Prescriptions""(""Id"") ON DELETE CASCADE,
                    ""MedicationName"" varchar(200) NOT NULL,
                    ""Dosage"" varchar(100),
                    ""Frequency"" varchar(150),
                    ""Duration"" varchar(100),
                    ""Instructions"" text,
                    ""DisplayOrder"" int NOT NULL DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS ""DentalDrugCatalogItems"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""TradeName"" varchar(200) NOT NULL,
                    ""ScientificName"" varchar(200),
                    ""Category"" varchar(100),
                    ""DefaultDosage"" varchar(100),
                    ""DefaultFrequency"" varchar(150),
                    ""DefaultDuration"" varchar(100),
                    ""DefaultInstructions"" text,
                    ""IsCommon"" boolean NOT NULL DEFAULT true
                );
            ");
        }
        else
        {
            // SQLite Migration Fallback
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS DentalToothRecords (
                    Id TEXT NOT NULL PRIMARY KEY,
                    PatientId TEXT NOT NULL,
                    ToothNumber INTEGER NOT NULL,
                    Condition INTEGER NOT NULL DEFAULT 1,
                    AffectedSurfaces TEXT,
                    Notes TEXT,
                    EstimatedCost NUMERIC NOT NULL DEFAULT 0,
                    IsCompleted INTEGER NOT NULL DEFAULT 0,
                    UpdatedAt TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS PatientDentalImages (
                    Id TEXT NOT NULL PRIMARY KEY,
                    PatientId TEXT NOT NULL,
                    Title TEXT NOT NULL,
                    ImageType INTEGER NOT NULL DEFAULT 7,
                    ImageUrl TEXT NOT NULL,
                    ThumbnailUrl TEXT,
                    Notes TEXT,
                    AssociatedToothNumber INTEGER,
                    TakenAt TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Prescriptions (
                    Id TEXT NOT NULL PRIMARY KEY,
                    PrescriptionNumber TEXT NOT NULL,
                    PatientId TEXT NOT NULL,
                    AppointmentId TEXT,
                    DoctorName TEXT NOT NULL,
                    Diagnosis TEXT,
                    GeneralInstructions TEXT,
                    IssuedAt TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS PrescriptionItems (
                    Id TEXT NOT NULL PRIMARY KEY,
                    PrescriptionId TEXT NOT NULL,
                    MedicationName TEXT NOT NULL,
                    Dosage TEXT,
                    Frequency TEXT,
                    Duration TEXT,
                    Instructions TEXT,
                    DisplayOrder INTEGER NOT NULL DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS DentalDrugCatalogItems (
                    Id TEXT NOT NULL PRIMARY KEY,
                    TradeName TEXT NOT NULL,
                    ScientificName TEXT,
                    Category TEXT,
                    DefaultDosage TEXT,
                    DefaultFrequency TEXT,
                    DefaultDuration TEXT,
                    DefaultInstructions TEXT,
                    IsCommon INTEGER NOT NULL DEFAULT 1
                );
            ");

            try
            {
                db.Database.ExecuteSqlRaw("ALTER TABLE Patients ADD COLUMN PasswordHash TEXT;");
            }
            catch { }
        }

        // بذر خدمات العيادة والأسعار الافتراضية
        if (!db.ClinicServices.Any())
        {
            db.ClinicServices.AddRange(
                new ClinicService { Id = Guid.NewGuid(), Name = "كشف واستشارة طبية", DefaultPrice = 250m, Category = "كشف وفحص", DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow },
                new ClinicService { Id = Guid.NewGuid(), Name = "حشو أسنان كمبوزيت", DefaultPrice = 500m, Category = "علاج وتجميل", DisplayOrder = 2, IsActive = true, CreatedAt = DateTime.UtcNow },
                new ClinicService { Id = Guid.NewGuid(), Name = "علاج جذور وعصب", DefaultPrice = 800m, Category = "علاج وتجميل", DisplayOrder = 3, IsActive = true, CreatedAt = DateTime.UtcNow },
                new ClinicService { Id = Guid.NewGuid(), Name = "تنظيف وتلميع أسنان وتكلسات", DefaultPrice = 400m, Category = "وقاية وتجميل", DisplayOrder = 4, IsActive = true, CreatedAt = DateTime.UtcNow },
                new ClinicService { Id = Guid.NewGuid(), Name = "تبييض أسنان احترافي", DefaultPrice = 1500m, Category = "وقاية وتجميل", DisplayOrder = 5, IsActive = true, CreatedAt = DateTime.UtcNow },
                new ClinicService { Id = Guid.NewGuid(), Name = "تركيبات وتيجان زيركون", DefaultPrice = 2500m, Category = "تركيبات", DisplayOrder = 6, IsActive = true, CreatedAt = DateTime.UtcNow },
                new ClinicService { Id = Guid.NewGuid(), Name = "زراعة أسنان", DefaultPrice = 5000m, Category = "جراحة وزراعة", DisplayOrder = 7, IsActive = true, CreatedAt = DateTime.UtcNow },
                new ClinicService { Id = Guid.NewGuid(), Name = "تقويم أسنان", DefaultPrice = 10000m, Category = "تقويم", DisplayOrder = 8, IsActive = true, CreatedAt = DateTime.UtcNow },
                new ClinicService { Id = Guid.NewGuid(), Name = "خلع ضرس وجراحة", DefaultPrice = 350m, Category = "جراحة وزراعة", DisplayOrder = 9, IsActive = true, CreatedAt = DateTime.UtcNow }
            );
            db.SaveChanges();
        }

        // بذر حسابات المدير والمساعد الافتراضية
        if (!db.AppUsers.Any(u => u.Username == "dr"))
        {
            db.AppUsers.Add(new AppUser
            {
                Username = "dr",
                PasswordHash = PasswordHasher.HashPassword("123"),
                FullName = "د. صديق (مدير المنظومة)",
                PhoneNumber = "01126092725",
                Role = UserRole.Manager,
                CanViewFinancials = true,
                CanManageExpenses = true,
                CanCancelExpenses = true,
                CanManageAppointments = true,
                CanManagePatients = true,
                CanExportReports = true,
                CanManageUsers = true,
                IsActive = true
            });
        }

        if (!db.AppUsers.Any(u => u.Username == "admin"))
        {
            db.AppUsers.Add(new AppUser
            {
                Username = "admin",
                PasswordHash = PasswordHasher.HashPassword("123"),
                FullName = "د. صديق (مدير المنظومة)",
                PhoneNumber = "01126092725",
                Role = UserRole.Manager,
                CanViewFinancials = true,
                CanManageExpenses = true,
                CanCancelExpenses = true,
                CanManageAppointments = true,
                CanManagePatients = true,
                CanExportReports = true,
                CanManageUsers = true,
                IsActive = true
            });
        }

        if (!db.AppUsers.Any(u => u.Username == "assistant"))
        {
            db.AppUsers.Add(new AppUser
            {
                Username = "assistant",
                PasswordHash = PasswordHasher.HashPassword("assistant123"),
                FullName = "مساعد العيادة (الاستقبال)",
                PhoneNumber = "01100000000",
                Role = UserRole.Assistant,
                CanViewFinancials = false,
                CanManageExpenses = true,
                CanCancelExpenses = false,
                CanManageAppointments = true,
                CanManagePatients = true,
                CanExportReports = false,
                CanManageUsers = false,
                IsActive = true
            });
        }

        db.SaveChanges();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Could not initialize database tables and seed users automatically.");
    }
}

app.UseCors("AllowAll");
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new 
{ 
    app = "Seddik Clinic Medical API", 
    status = "Active", 
    time = DateTime.UtcNow 
}));

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds + "ms",
            checkedAtUtc = DateTime.UtcNow,
            entries = report.Entries.Select(e => new
            {
                key = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                data = e.Value.Data
            })
        };
        await context.Response.WriteAsJsonAsync(response);
    }
});

app.MapGet("/liveness", () => Results.Ok(new { status = "Awake", utcTime = DateTime.UtcNow, message = "خادم عيادة صديق يعمل بكفاءة" }));

app.MapControllers();

app.Run();
