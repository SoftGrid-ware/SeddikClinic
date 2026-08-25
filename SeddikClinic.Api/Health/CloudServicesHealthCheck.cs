using Microsoft.Extensions.Diagnostics.HealthChecks;
using SeddikClinic.Core.Interfaces;
using SeddikClinic.Infrastructure.Data;

namespace SeddikClinic.Api.Health;

public class CloudServicesHealthCheck : IHealthCheck
{
    private readonly SeddikClinicDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;

    public CloudServicesHealthCheck(SeddikClinicDbContext dbContext, IFileStorageService fileStorageService)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        bool isDbHealthy = false;
        bool isStorageHealthy = false;

        // 1. فحص قاعدة البيانات السحابية (PostgreSQL / Neon)
        try
        {
            isDbHealthy = await _dbContext.Database.CanConnectAsync(cancellationToken);
            data["DatabaseStatus"] = isDbHealthy ? "Connected (Healthy)" : "Cannot Connect";
        }
        catch (Exception ex)
        {
            data["DatabaseStatus"] = $"Failed: {ex.Message}";
        }

        // 2. فحص سحابة التخزين (Cloudflare R2) وحصص الاستهلاك
        try
        {
            var quota = await _fileStorageService.GetStorageUsageSummaryAsync();
            isStorageHealthy = true;
            data["StorageUsedBytes"] = quota.TotalUsedBytes;
            data["StorageUsedFormatted"] = quota.FormattedUsedSize;
            data["StorageUsedPercentage"] = $"{quota.UsedPercentage}%";
            data["StorageLimitStatus"] = quota.IsApproachingLimit ? "Warning: Approaching 80%" : "OK";
        }
        catch (Exception ex)
        {
            data["StorageStatus"] = $"Failed: {ex.Message}";
        }

        data["ServerUtcTime"] = DateTime.UtcNow;
        data["Environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        if (isDbHealthy && isStorageHealthy)
        {
            return HealthCheckResult.Healthy("جميع الخدمات السحابية وقاعدة البيانات تعمل بكفاءة.", data);
        }

        if (isDbHealthy)
        {
            return HealthCheckResult.Degraded("قاعدة البيانات تعمل، ولكن خدمة التخزين السحابي تواجه مشكلة.", null, data);
        }

        return HealthCheckResult.Unhealthy("فشل الاتصال بقاعدة البيانات السحابية.", null, data);
    }
}
