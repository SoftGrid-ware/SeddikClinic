using SeddikClinic.Core.Enums;

namespace SeddikClinic.Core.Entities.Financial;

public class FinancialAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityName { get; set; } = string.Empty; // Expenses, FinancialPeriods, Budgets, Payments
    public string RecordId { get; set; } = string.Empty;
    public FinancialAuditAction Action { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? DeviceInfo { get; set; }
    public string? Remarks { get; set; }
}
