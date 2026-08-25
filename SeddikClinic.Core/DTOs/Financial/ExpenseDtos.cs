using SeddikClinic.Core.Enums;

namespace SeddikClinic.Core.DTOs.Financial;

public class ExpenseDto
{
    public Guid Id { get; set; }
    public string ExpenseNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryNameAr { get; set; } = string.Empty;
    public string? CategoryColorHex { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public ExpensePaymentMethod PaymentMethod { get; set; }
    public string PaymentMethodNameAr { get; set; } = string.Empty;
    public ExpenseRecurrenceType RecurrenceType { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }
    public ExpenseStatus Status { get; set; }
    public string StatusNameAr { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public Guid? DoctorId { get; set; }
    public string? DoctorName { get; set; }
    public List<ExpenseAttachmentDto> Attachments { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;
}

public class CreateExpenseDto
{
    public string Title { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public ExpensePaymentMethod PaymentMethod { get; set; } = ExpensePaymentMethod.Cash;
    public ExpenseRecurrenceType RecurrenceType { get; set; } = ExpenseRecurrenceType.OneTime;
    public string? BeneficiaryName { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }
    public ExpenseStatus Status { get; set; } = ExpenseStatus.Paid;
    public Guid BranchId { get; set; }
    public Guid? DoctorId { get; set; }
}

public class UpdateExpenseDto
{
    public string Title { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public ExpensePaymentMethod PaymentMethod { get; set; }
    public ExpenseRecurrenceType RecurrenceType { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }
    public ExpenseStatus Status { get; set; }
    public Guid? DoctorId { get; set; }
}

public class CancelExpenseDto
{
    public string CancellationReason { get; set; } = string.Empty;
}

public class ExpenseFilterDto
{
    public string? SearchTerm { get; set; }
    public Guid? CategoryId { get; set; }
    public ExpenseStatus? Status { get; set; }
    public ExpensePaymentMethod? PaymentMethod { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public Guid? DoctorId { get; set; }
    public Guid? BranchId { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ExpenseAttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
}

public class ExpenseCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? ColorHex { get; set; }
    public bool IsActive { get; set; }
    public bool IsDirectCost { get; set; }
}
