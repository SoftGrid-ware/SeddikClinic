using SeddikClinic.Core.Enums;

namespace SeddikClinic.Core.DTOs.Financial;

public class DailyShiftSummaryDto
{
    public Guid Id { get; set; }
    public string ShiftNumber { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public DateTime ShiftDate { get; set; }
    public DailyShiftType ShiftType { get; set; }
    public DailyShiftStatus Status { get; set; }

    public DateTime OpenedAt { get; set; }
    public string OpenedByUserId { get; set; } = string.Empty;
    public string OpenedByUserName { get; set; } = string.Empty;
    public decimal OpeningCashBalance { get; set; }

    public DateTime? ClosedAt { get; set; }
    public string? ClosedByUserId { get; set; }
    public string? ClosedByUserName { get; set; }

    public decimal TotalCashRevenue { get; set; }
    public decimal TotalCardRevenue { get; set; }
    public decimal TotalTransferRevenue { get; set; }
    public decimal TotalInstallmentsCollected { get; set; }
    public decimal TotalRevenue => TotalCashRevenue + TotalCardRevenue + TotalTransferRevenue;

    public decimal TotalCashExpenses { get; set; }
    public decimal TotalRefunds { get; set; }

    public decimal ExpectedCashInDrawer { get; set; }
    public decimal ActualCashInDrawer { get; set; }
    public decimal DifferenceAmount { get; set; }
    public ShiftDifferenceStatus DifferenceStatus { get; set; }
    public string? DifferenceReason { get; set; }
    public string? HandoverNotes { get; set; }
    public string? HandoverToUserName { get; set; }

    public int AppointmentsCount { get; set; }
    public int CompletedAppointmentsCount { get; set; }
    public int InvoicesCount { get; set; }

    public bool IsClosed => Status == DailyShiftStatus.Closed;
}

public class OpenShiftRequestDto
{
    public Guid BranchId { get; set; }
    public DailyShiftType ShiftType { get; set; } = DailyShiftType.FullDay;
    public decimal OpeningCashBalance { get; set; } = 0m;
    public string? Notes { get; set; }
}

public class CloseShiftRequestDto
{
    public Guid ShiftId { get; set; }
    public decimal ActualCashInDrawer { get; set; }
    public string? DifferenceReason { get; set; }
    public string? HandoverNotes { get; set; }
    public string? HandoverToUserName { get; set; }
}
