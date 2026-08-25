using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SeddikClinic.Api.BackgroundServices;
using SeddikClinic.Api.Health;
using SeddikClinic.Core.Interfaces;
using SeddikClinic.Infrastructure.Data;
using SeddikClinic.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// دعم منفذ السحابة المخصص
var cloudPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(cloudPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{cloudPort}");
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

// 3. خدمة المصروفات الشهرية التلقائية
builder.Services.AddHostedService<RecurringExpensesWorker>();

// 4. الفحص الصحي
builder.Services.AddHealthChecks()
    .AddCheck<CloudServicesHealthCheck>("CloudAndDatabaseHealth");

// 5. إعداد المصادقة والتفويض (JWT Authentication & Authorization)
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

// إنشاء الجداول وبذر البيانات
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<SeddikClinicDbContext>();
        db.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Could not initialize database automatically.");
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
