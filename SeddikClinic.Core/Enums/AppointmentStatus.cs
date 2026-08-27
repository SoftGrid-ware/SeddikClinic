namespace SeddikClinic.Core.Enums;

public enum AppointmentStatus
{
    Scheduled = 1,    // مجدول / محجوز
    Confirmed = 2,    // مؤكد بالحضور
    Waiting = 3,      // في صالة الانتظار
    InProgress = 4,   // داخل غرفة الكشف مع الطبيب
    Completed = 5,    // تم الانتهاء من الكشف
    Cancelled = 6,    // ملغي
    NoShow = 7        // لم يحضر
}
