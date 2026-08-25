using SeddikClinic.Core.Enums;

namespace SeddikClinic.Core.Entities.Financial;

public class Expense
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExpenseNumber { get; set; } = string.Empty; // مثل EXP-202608-0001
    public string Title { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public ExpenseCategory? Category { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public ExpensePaymentMethod PaymentMethod { get; set; } = ExpensePaymentMethod.Cash;
    public ExpenseRecurrenceType RecurrenceType { get; set; } = ExpenseRecurrenceType.OneTime;
    public string? BeneficiaryName { get; set; } // المورد أو المستفيد
    public string? ReceiptNumber { get; set; }   // رقم الفاتورة أو الإيصال
    public string? Notes { get; set; }
    public ExpenseStatus Status { get; set; } = ExpenseStatus.Paid;

    // الربط مع الفرع والطبيب
    public Guid BranchId { get; set; }
    public Guid? DoctorId { get; set; } // في حال كان المصروف خاصاً بطبيب معين
    public Guid? RecurringExpenseId { get; set; } // إذا تم توليده آلياً من مصروف دوري

    // المرفقات
    public ICollection<ExpenseAttachment> Attachments { get; set; } = new List<ExpenseAttachment>();

    // حقول التتبع والتدقيق (Audit)
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByUserName { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }
    public string? CancellationReason { get; set; }
}
