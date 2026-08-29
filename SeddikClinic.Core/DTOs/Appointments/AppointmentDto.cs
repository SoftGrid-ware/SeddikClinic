using SeddikClinic.Core.Enums;

namespace SeddikClinic.Core.DTOs.Appointments;

public class AppointmentDto
{
    public Guid Id { get; set; }
    public string AppointmentNumber { get; set; } = string.Empty;
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientPhone { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public DateTime AppointmentDate { get; set; }
    public string FormattedTime { get; set; } = string.Empty;
    public string StartTimeFormatted => DateTime.Today.Add(StartTime).ToString("hh:mm tt");
    public string DateFormatted => AppointmentDate.ToString("yyyy/MM/dd");
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string ServiceType { get; set; } = string.Empty;
    public AppointmentStatus Status { get; set; }
    public string StatusNameAr => Status switch
    {
        AppointmentStatus.Scheduled => "مجدول",
        AppointmentStatus.Confirmed => "مؤكد",
        AppointmentStatus.Waiting => "في صالة الانتظار",
        AppointmentStatus.InProgress => "داخل العيادة",
        AppointmentStatus.Completed => "تم الكشف",
        AppointmentStatus.Cancelled => "ملغي",
        AppointmentStatus.NoShow => "لم يحضر",
        _ => Status.ToString()
    };
    public string StatusColorHex => Status switch
    {
        AppointmentStatus.Scheduled => "#2563EB",
        AppointmentStatus.Confirmed => "#059669",
        AppointmentStatus.Waiting => "#D97706",
        AppointmentStatus.InProgress => "#7C3AED",
        AppointmentStatus.Completed => "#16A34A",
        AppointmentStatus.Cancelled => "#DC2626",
        AppointmentStatus.NoShow => "#64748B",
        _ => "#94A3B8"
    };
    public string StatusBackgroundHex => Status switch
    {
        AppointmentStatus.Scheduled => "#EFF6FF",
        AppointmentStatus.Confirmed => "#ECFDF5",
        AppointmentStatus.Waiting => "#FEF3C7",
        AppointmentStatus.InProgress => "#F5F3FF",
        AppointmentStatus.Completed => "#DCFCE7",
        AppointmentStatus.Cancelled => "#FEE2E2",
        AppointmentStatus.NoShow => "#F1F5F9",
        _ => "#F1F5F9"
    };
    public decimal TotalFees { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetFees => Math.Max(0, TotalFees - DiscountAmount);
    public decimal DepositAmount { get; set; }
    public decimal RemainingAmount => Math.Max(0, NetFees - DepositAmount);
    public bool IsDepositPaid { get; set; }
    public string? Notes { get; set; }
    public string? ReasonForVisit { get; set; }
    public string? CancellationReason { get; set; }

    public string SourceText => (Notes != null && Notes.Contains("تطبيق المريض")) ? "تطبيق المريض" : "العيادة";
    public bool CanCancel => Status == AppointmentStatus.Scheduled || Status == AppointmentStatus.Confirmed;
}

public class CreateAppointmentDto
{
    public Guid? PatientId { get; set; }
    public string? NewPatientFullName { get; set; }
    public string? NewPatientPhone { get; set; }
    public Guid? DoctorId { get; set; }
    public string DoctorName { get; set; } = "د. صديق";
    public DateTime AppointmentDate { get; set; }
    public string? StartTimeString { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public string ServiceType { get; set; } = "كشف عام";
    public decimal TotalFees { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public string? Notes { get; set; }
    public string? ReasonForVisit { get; set; }
}

public class UpdateAppointmentStatusDto
{
    public AppointmentStatus Status { get; set; }
    public string? CancellationReason { get; set; }
}

public class UpdateAppointmentServiceDto
{
    public string ServiceType { get; set; } = string.Empty;
    public decimal? TotalFees { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? DepositAmount { get; set; }
    public string? Notes { get; set; }
}

public class RescheduleAppointmentDto
{
    public DateTime NewDate { get; set; }
    public string StartTimeString { get; set; } = string.Empty;
    public string? NewStartTime { get; set; }
    public int DurationMinutes { get; set; } = 30;
}

public class AppointmentSummaryDto
{
    public int TotalScheduledToday { get; set; }
    public int CompletedToday { get; set; }
    public int WaitingCount { get; set; }
    public int InProgressCount { get; set; }
    public int CancelledToday { get; set; }

    public int TotalToday { get => TotalScheduledToday; set => TotalScheduledToday = value; }
    public int CompletedCount { get => CompletedToday; set => CompletedToday = value; }
    public int CancelledCount { get => CancelledToday; set => CancelledToday = value; }
    public List<AppointmentDto> TodayAppointments { get; set; } = new();
}
