using SeddikClinic.Core.Enums;

namespace SeddikClinic.Core.Entities.Financial;

public class FinancialPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public FinancialPeriodStatus Status { get; set; } = FinancialPeriodStatus.Open;

    // تفاصيل الإقفال
    public FinancialPeriodClosing? ClosingDetails { get; set; }
}
