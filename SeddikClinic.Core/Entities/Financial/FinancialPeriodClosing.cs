namespace SeddikClinic.Core.Entities.Financial;

public class FinancialPeriodClosing
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PeriodId { get; set; }
    public FinancialPeriod? Period { get; set; }

    public string ClosedByUserId { get; set; } = string.Empty;
    public string ClosedByUserName { get; set; } = string.Empty;
    public DateTime ClosedAt { get; set; } = DateTime.UtcNow;

    // الأرقام المالية المثبتة وقت الإقفال
    public decimal TotalRevenueCollected { get; set; }
    public decimal TotalExpensesPaid { get; set; }
    public decimal NetCashFlow { get; set; }
    public decimal TotalUncollectedReceivables { get; set; }
    public decimal TotalAccruedExpenses { get; set; }
    public string? Notes { get; set; }

    // سجل إعادة الفتح الطارئ (Emergency Reopening by Admin/Owner)
    public bool IsReopened { get; set; } = false;
    public string? ReopenedByUserId { get; set; }
    public string? ReopenedByUserName { get; set; }
    public DateTime? ReopenedAt { get; set; }
    public string? ReopenReason { get; set; }
}
