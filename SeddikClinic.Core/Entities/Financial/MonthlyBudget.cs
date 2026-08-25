namespace SeddikClinic.Core.Entities.Financial;

public class MonthlyBudget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CategoryId { get; set; }
    public ExpenseCategory? Category { get; set; }
    public Guid BranchId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; } // 1 - 12
    public decimal BudgetAmount { get; set; }
    public int AlertThresholdPercent { get; set; } = 85; // تنبيه عند تجاوز 85% من الميزانية
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
}
