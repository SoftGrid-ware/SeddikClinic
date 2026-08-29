using SeddikClinic.Core.Enums;

namespace SeddikClinic.Core.Entities.Financial;

public class DailyShift
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ShiftNumber { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public DateTime ShiftDate { get; set; }
    public DailyShiftType ShiftType { get; set; } = DailyShiftType.FullDay;
    public DailyShiftStatus Status { get; set; } = DailyShiftStatus.Open;

    // Opening Details
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public string OpenedByUserId { get; set; } = string.Empty;
    public string OpenedByUserName { get; set; } = string.Empty;
    public decimal OpeningCashBalance { get; set; } = 0m; // عهدة بداية الوردية

    // Closing Details
    public DateTime? ClosedAt { get; set; }
    public string? ClosedByUserId { get; set; }
    public string? ClosedByUserName { get; set; }

    // System Calculated Financials
    public decimal TotalCashRevenue { get; set; } = 0m;       // مقبوضات كاش
    public decimal TotalCardRevenue { get; set; } = 0m;       // مدفوعات فيزا/بطاقة
    public decimal TotalTransferRevenue { get; set; } = 0m;   // تحويلات ومحافظ إلكترونية
    public decimal TotalInstallmentsCollected { get; set; } = 0m; // أقساط وعربين محصلة
    public decimal TotalCashExpenses { get; set; } = 0m;      // مصروفات نقدية مسددة
    public decimal TotalRefunds { get; set; } = 0m;           // مستردات للمرضى
    public decimal ExpectedCashInDrawer { get; set; } = 0m;   // النقد المتوقع بالدرج

    // Cash Drawer Physical Count & Reconciliation
    public decimal ActualCashInDrawer { get; set; } = 0m;     // النقد الفعلي المعدود
    public decimal DifferenceAmount { get; set; } = 0m;       // الفارق (فعلي - متوقع)
    public ShiftDifferenceStatus DifferenceStatus { get; set; } = ShiftDifferenceStatus.Balanced;
    public string? DifferenceReason { get; set; }
    public string? HandoverNotes { get; set; }
    public string? HandoverToUserName { get; set; }

    // Shift Operations Summary
    public int AppointmentsCount { get; set; } = 0;
    public int CompletedAppointmentsCount { get; set; } = 0;
    public int InvoicesCount { get; set; } = 0;
}
