using SeddikClinic.Core.Enums;

namespace SeddikClinic.Core.Entities.Billing;

public class PatientRefund
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RefundNumber { get; set; } = string.Empty;
    public Guid InvoiceId { get; set; }
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public Guid BranchId { get; set; }
    public decimal Amount { get; set; } // المبلغ المسترد
    public DateTime RefundDate { get; set; } = DateTime.UtcNow;
    public ExpensePaymentMethod RefundMethod { get; set; } = ExpensePaymentMethod.Cash;
    public string Reason { get; set; } = string.Empty;
    public string AuthorizedByUserId { get; set; } = string.Empty;
}
