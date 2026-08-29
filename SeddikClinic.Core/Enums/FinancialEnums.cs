namespace SeddikClinic.Core.Enums;

public enum ExpensePaymentMethod
{
    Cash = 1,              // نقداً
    DebitCreditCard = 2,   // بطاقة مدى / ائتمان
    BankTransfer = 3,      // تحويل بنكي
    Cheque = 4             // شيك
}

public enum ExpenseRecurrenceType
{
    OneTime = 1,   // لمرة واحدة
    Daily = 2,     // يومي
    Monthly = 3,   // شهري متكرر
    Periodic = 4   // دوري مخصص
}

public enum ExpenseStatus
{
    Paid = 1,      // مدفوع (يخصم من صافي التدفق النقدي)
    Accrued = 2,   // مستحق غير مدفوع (التزام لا يخصم حتى يسدد)
    Cancelled = 3, // ملغي
    Refunded = 4   // مسترد
}

public enum FinancialPeriodStatus
{
    Open = 1,   // فترة مفتوحة قابلة للتسجيل والتعديل
    Closed = 2  // فترة مقفلة ماليًا لا يمكن التعديل عليها إلا للمدير
}

public enum FinancialAuditAction
{
    Create = 1,
    Update = 2,
    Cancel = 3,
    StatusChange = 4,
    PeriodClose = 5,
    PeriodReopen = 6,
    ShiftOpen = 7,
    ShiftClose = 8
}

public enum DailyShiftStatus
{
    Open = 1,   // وردية مفتوحة جارية
    Closed = 2  // وردية مقفلة ومطابقة
}

public enum DailyShiftType
{
    Morning = 1,  // وردية صباحية
    Evening = 2,  // وردية مسائية
    FullDay = 3   // وردية يوم كامل
}

public enum ShiftDifferenceStatus
{
    Balanced = 1, // متطابق تماماً
    Surplus = 2,  // زيادة في الصندوق
    Shortage = 3  // عجز في الصندوق
}
