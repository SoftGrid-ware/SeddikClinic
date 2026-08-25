using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Core.Entities.Financial;
using SeddikClinic.Core.Enums;
using SeddikClinic.Core.Interfaces;
using SeddikClinic.Infrastructure.Data;

namespace SeddikClinic.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly SeddikClinicDbContext _dbContext;

    public AuditLogService(SeddikClinicDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task LogAsync(
        string entityName, 
        string recordId, 
        FinancialAuditAction action, 
        object? oldValues, 
        object? newValues, 
        string userId, 
        string userName, 
        string? ipAddress, 
        string? deviceInfo, 
        string? remarks = null)
    {
        var log = new FinancialAuditLog
        {
            EntityName = entityName,
            RecordId = recordId,
            Action = action,
            OldValuesJson = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
            NewValuesJson = newValues != null ? JsonSerializer.Serialize(newValues) : null,
            UserId = userId,
            UserName = userName,
            Timestamp = DateTime.UtcNow,
            IpAddress = ipAddress,
            DeviceInfo = deviceInfo,
            Remarks = remarks
        };

        _dbContext.FinancialAuditLogs.Add(log);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<IEnumerable<FinancialAuditLogDto>> GetLogsForRecordAsync(string entityName, string recordId)
    {
        var logs = await _dbContext.FinancialAuditLogs
            .AsNoTracking()
            .Where(l => l.EntityName == entityName && l.RecordId == recordId)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();

        return logs.Select(l => new FinancialAuditLogDto
        {
            Id = l.Id,
            EntityName = l.EntityName,
            RecordId = l.RecordId,
            ActionTypeNameAr = l.Action switch
            {
                FinancialAuditAction.Create => "إنشاء",
                FinancialAuditAction.Update => "تعديل",
                FinancialAuditAction.Cancel => "إلغاء",
                FinancialAuditAction.StatusChange => "تغيير حالة",
                FinancialAuditAction.PeriodClose => "إقفال فترة مالية",
                FinancialAuditAction.PeriodReopen => "إعادة فتح فترة مالية",
                _ => l.Action.ToString()
            },
            OldValuesJson = l.OldValuesJson,
            NewValuesJson = l.NewValuesJson,
            UserName = l.UserName,
            Timestamp = l.Timestamp,
            IpAddress = l.IpAddress,
            DeviceInfo = l.DeviceInfo,
            Remarks = l.Remarks
        });
    }
}
