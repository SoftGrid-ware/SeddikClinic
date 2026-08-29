using SeddikClinic.Core.Enums;

namespace SeddikClinic.Core.Entities.Appointments;

public class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AppointmentNumber { get; set; } = string.Empty; // رقم الحجز مثلا APT-202608-001
    
    // المريض
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    // الطبيب والفرع
    public Guid? DoctorId { get; set; }
    public string DoctorName { get; set; } = "د. صديق";
    public Guid? BranchId { get; set; }

    // التوقيت
    public DateTime AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    // نوع الخدمة
    public string ServiceType { get; set; } = "كشف واستشارة"; // زراعة أسنان، تقويم، حشو، تنظيف، جراحة
    public string? ReasonForVisit { get; set; }

    // الحالة
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

    // الأمور المالية المرتبطة بالحجز
    public decimal TotalFees { get; set; } = 0m;
    public decimal DiscountAmount { get; set; } = 0m; // الخصم المالي الممنوح للمريض
    public decimal DepositAmount { get; set; } = 0m; // العربون المدفوع
    public bool IsDepositPaid { get; set; } = false;

    // الملاحظات وسجل التدقيق
    public string? Notes { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserName { get; set; }
    public bool IsDeleted { get; set; } = false;
}
