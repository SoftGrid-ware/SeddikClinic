namespace SeddikClinic.Core.Entities.Financial;

public class ExpenseCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Icon { get; set; }
    public string? ColorHex { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDirectCost { get; set; } // تكلفة مباشرة (معمل، مواد طبية) لحساب الربح التشغيلي
    public int DisplayOrder { get; set; } = 0;

    // العلاقات
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<RecurringExpense> RecurringExpenses { get; set; } = new List<RecurringExpense>();
    public ICollection<MonthlyBudget> MonthlyBudgets { get; set; } = new List<MonthlyBudget>();
}
