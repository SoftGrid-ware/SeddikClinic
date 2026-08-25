namespace SeddikClinic.Core.DTOs.Financial;

public class MonthlyBudgetDto
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryNameAr { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal BudgetAmount { get; set; }
    public decimal ActualSpentAmount { get; set; }
    public decimal RemainingAmount => BudgetAmount - ActualSpentAmount;
    public decimal UsagePercentage => BudgetAmount > 0 ? Math.Round((ActualSpentAmount / BudgetAmount) * 100, 1) : 0;
    public int AlertThresholdPercent { get; set; }
    public bool IsAlertTriggered => UsagePercentage >= AlertThresholdPercent;
}

public class SetMonthlyBudgetDto
{
    public Guid CategoryId { get; set; }
    public Guid BranchId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal BudgetAmount { get; set; }
    public int AlertThresholdPercent { get; set; } = 85;
}
