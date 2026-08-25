using SeddikClinic.Core.Enums;

namespace SeddikClinic.Core.DTOs.Financial;

public class FinancialPeriodDto
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public FinancialPeriodStatus Status { get; set; }
    public string StatusNameAr => Status == FinancialPeriodStatus.Open ? "مفتوحة" : "مقفلة";
    public FinancialPeriodClosingDto? ClosingDetails { get; set; }
}

public class ClosePeriodDto
{
    public Guid PeriodId { get; set; }
    public string? Notes { get; set; }
}

public class ReopenPeriodDto
{
    public Guid PeriodId { get; set; }
    public string ReopenReason { get; set; } = string.Empty;
}

public class FinancialPeriodClosingDto
{
    public Guid Id { get; set; }
    public string ClosedByUserName { get; set; } = string.Empty;
    public DateTime ClosedAt { get; set; }
    public decimal TotalRevenueCollected { get; set; }
    public decimal TotalExpensesPaid { get; set; }
    public decimal NetCashFlow { get; set; }
    public decimal TotalUncollectedReceivables { get; set; }
    public decimal TotalAccruedExpenses { get; set; }
    public string? Notes { get; set; }
    public bool IsReopened { get; set; }
    public string? ReopenedByUserName { get; set; }
    public DateTime? ReopenedAt { get; set; }
    public string? ReopenReason { get; set; }
}

public class FinancialAuditLogDto
{
    public Guid Id { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public string ActionTypeNameAr { get; set; } = string.Empty;
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
    public string? DeviceInfo { get; set; }
    public string? Remarks { get; set; }
}
