namespace SeddikClinic.Core.Enums;

public enum UserRole
{
    Manager = 1,    // المدير / الطبيب الرئيسي (كامل الصلاحيات)
    Assistant = 2,  // المساعد / موظف الاستقبال (صلاحيات مخصصة)
    Doctor = 3      // طبيب أخصائي متعاون
}
