using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using SeddikClinic.Api.BackgroundServices;
using SeddikClinic.Api.Health;
using SeddikClinic.Core.Interfaces;
using SeddikClinic.Infrastructure.Data;
using SeddikClinic.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. إعداد قاعدة البيانات (يدعم PostgreSQL سحابياً أو SQLite محلياً لسهولة التشغيل الفوري)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? builder.Configuration["DATABASE_URL"]
    ?? "Data Source=seddik_clinic_local.db";

// إذا كان الاتصال محلياً بـ SQLite أو لا يحتوي على رابط postgres
var isSqlite = connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase) || 
               connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase);

if (!isSqlite)
{
    // دعم صيغة postgres:// من Render/Fly.io/Neon تلقائياً
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

// 2. تسجيل الخدمات والطبقات المعمارية (Clean Architecture DI)
builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();
builder.Services.AddScoped<IFileStorageService, CloudflareR2StorageService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IFinancialPeriodService, FinancialPeriodService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IFinancialReportService, FinancialReportService>();

// 3. تسجيل الخدمة الخلفية الدورية للمصروفات الشهرية (Background Worker)
builder.Services.AddHostedService<RecurringExpensesWorker>();

// 4. إعداد الفحص الصحي (Health Checks) للسيرفر وقاعدة البيانات والسحابة
builder.Services.AddHealthChecks()
    .AddCheck<CloudServicesHealthCheck>("CloudAndDatabaseHealth");

// 5. إعداد CORS للسماح بالاتصال من تطبيقات الويب وسطح المكتب والموبايل
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 6. تسجيل Controllers مع دعم تسلسل الـ Enums
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var app = builder.Build();

// إنشاء الجداول وبذر البيانات تلقائياً عند الإقلاع
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
        logger.LogWarning(ex, "Could not initialize database automatically. Make sure the database provider is ready.");
    }
}

app.UseCors("AllowAll");
app.UseStaticFiles(); // لدعم الملفات المرفوعة في وضع التطوير المحلي

app.UseRouting();
app.UseAuthorization();

// نقاط فحص الاستيقاظ والصحة
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

// نقطة سريعة جداً للـ Keep-Alive Pings من cron-job.org
app.MapGet("/liveness", () => Results.Ok(new { status = "Awake", utcTime = DateTime.UtcNow, message = "خادم عيادة صديق يعمل بكفاءة" }));

app.MapControllers();

app.Run();
