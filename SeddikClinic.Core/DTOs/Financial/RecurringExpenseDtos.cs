namespace SeddikClinic.Core.DTOs.Financial;

public class RecurringExpenseDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryNameAr { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int DayOfMonth { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? LastGeneratedDate { get; set; }
    public bool AutoCreate { get; set; }
    public int AlertBeforeDays { get; set; }
    public bool IsActive { get; set; }
    public Guid BranchId { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? Notes { get; set; }
}

public class CreateRecurringExpenseDto
{
    public string Title { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public decimal Amount { get; set; }
    public int DayOfMonth { get; set; } = 1;
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    public bool AutoCreate { get; set; } = true;
    public int AlertBeforeDays { get; set; } = 3;
    public Guid BranchId { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? Notes { get; set; }
}
