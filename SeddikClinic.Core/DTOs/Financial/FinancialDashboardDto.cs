namespace SeddikClinic.Core.DTOs.Financial;

public class FinancialDashboardDto
{
    // أرقام اليوم
    public decimal TodayRevenue { get; set; }
    public decimal TodayExpenses { get; set; }
    public decimal TodayNetProfit { get; set; }

    // أرقام الشهر الحالي
    public decimal MonthRevenue { get; set; }
    public decimal MonthExpenses { get; set; }
    public decimal MonthNetProfit { get; set; }

    // إجماليات التحصيل والالتزامات
    public decimal TotalCollectedRevenue { get; set; }        // مجموع المدفوعات المحصلة فعلياً
    public decimal TotalUncollectedReceivables { get; set; } // إجمالي المبالغ المستحقة غير المحصلة
    public decimal TotalDownPayments { get; set; }           // قيمة العربون والدفعات الجزئية
    public decimal TotalRefunds { get; set; }                // المبالغ المستردة
    public decimal NetCashFlow { get; set; }                 // صافي التدفق النقدي = المحصل - المسترد - المصروفات المدفوعة
    public decimal EstimatedOperatingProfit { get; set; }    // الربح التشغيلي التقديري = إيرادات الخدمات - تكاليفها المباشرة

    // مقارنات ومؤشرات
    public decimal PreviousMonthRevenue { get; set; }
    public decimal PreviousMonthExpenses { get; set; }
    public decimal PreviousMonthNetProfit { get; set; }
    public decimal RevenueGrowthPercentage { get; set; }
    public decimal ProfitGrowthPercentage { get; set; }
    public decimal AverageDailyIncome { get; set; }

    // الرسوم البيانية والإحصائيات
    public List<TopServiceRevenueDto> TopRevenueServices { get; set; } = new();
    public List<TopEarningDayDto> TopRevenueDays { get; set; } = new();
    public List<DailyFinancialPointDto> DailyTrendChart { get; set; } = new();
    public List<CategoryExpenseBreakdownDto> CategoryExpenseBreakdown { get; set; } = new();
}

public class TopServiceRevenueDto
{
    public string ServiceName { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal PercentageOfTotal { get; set; }
}

public class TopEarningDayDto
{
    public DateTime Date { get; set; }
    public string DayNameAr { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class DailyFinancialPointDto
{
    public DateTime Date { get; set; }
    public string FormattedDate { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetProfit => Revenue - Expenses;
}

public class CategoryExpenseBreakdownDto
{
    public Guid CategoryId { get; set; }
    public string CategoryNameAr { get; set; } = string.Empty;
    public string? ColorHex { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PercentageOfTotal { get; set; }
    public decimal BudgetAmount { get; set; }
    public bool IsOverBudget => BudgetAmount > 0 && TotalAmount > BudgetAmount;
}

public class FinancialFilterDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? PeriodType { get; set; } // "today", "week", "month", "year", "custom"
    public Guid? DoctorId { get; set; }      // محدد لطبيب معين أو فارغ للعيادة بالكامل
    public Guid? BranchId { get; set; }
}
