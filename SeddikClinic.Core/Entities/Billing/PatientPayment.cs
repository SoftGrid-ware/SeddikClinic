using SeddikClinic.Core.Enums;

namespace SeddikClinic.Core.Entities.Billing;

public enum PaymentType
{
    FullPayment = 1,   // دفعة كاملة
    DownPayment = 2,   // عربون / دفعة مقدمة
    PartialPayment = 3 // دفعة جزئية
}

public class PatientPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ReceiptNumber { get; set; } = string.Empty;
    public Guid InvoiceId { get; set; }
    public PatientInvoice? Invoice { get; set; }
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public Guid BranchId { get; set; }
    public decimal Amount { get; set; } // القيمة المحصلة فعلياً
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public ExpensePaymentMethod PaymentMethod { get; set; } = ExpensePaymentMethod.Cash;
    public PaymentType PaymentType { get; set; } = PaymentType.FullPayment;
    public string? Notes { get; set; }
    public string ReceivedByUserId { get; set; } = string.Empty;
}
