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
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IClinicServiceCatalogService, ClinicServiceCatalogService>();

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
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""IsDeleted"" boolean NOT NULL DEFAULT false
                );

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
            ");
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
        if (!db.AppUsers.Any(u => u.Username == "admin"))
        {
            db.AppUsers.Add(new AppUser
            {
                Username = "admin",
                PasswordHash = PasswordHasher.HashPassword("admin123"),
                FullName = "د. صديق (مدير المنظومة)",
                PhoneNumber = "01000000000",
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
