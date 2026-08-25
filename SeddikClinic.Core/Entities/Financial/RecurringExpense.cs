namespace SeddikClinic.Core.Entities.Financial;

public class RecurringExpense
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public ExpenseCategory? Category { get; set; }
    public decimal Amount { get; set; }
    public int DayOfMonth { get; set; } = 1; // يوم الاستحقاق الشهري (1 - 31)
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    public DateTime? LastGeneratedDate { get; set; }
    public bool AutoCreate { get; set; } = true; // إنشاء تلقائي كـ Expense مستحق أو مدفوع
    public int AlertBeforeDays { get; set; } = 3; // إشعار قبل الاستحقاق بعدد X أيام
    public bool IsActive { get; set; } = true;
    public Guid BranchId { get; set; }
    public Guid? DoctorId { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
}
