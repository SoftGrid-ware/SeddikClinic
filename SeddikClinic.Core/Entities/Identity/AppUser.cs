using SeddikClinic.Core.Enums;

namespace SeddikClinic.Core.Entities.Identity;

public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public UserRole Role { get; set; } = UserRole.Assistant;

    // مصفوفة الصلاحيات الدقيقة (Granular Permissions)
    public bool CanViewFinancials { get; set; } = false;      // رؤية الأرباح والتحليلات والرسوم البيانية
    public bool CanManageExpenses { get; set; } = true;       // تسجيل المصروفات
    public bool CanCancelExpenses { get; set; } = false;      // إلغاء المصروفات
    public bool CanManageAppointments { get; set; } = true;   // حجز وتعديل المواعيد وصالة الانتظار
    public bool CanManagePatients { get; set; } = true;       // فتح وتعديل ملفات المرضى
    public bool CanExportReports { get; set; } = false;       // تصدير ملفات Excel والتقارير
    public bool CanManageUsers { get; set; } = false;         // إدارة المستخدمين والصلاحيات (خاص بالمدير)

    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
}
