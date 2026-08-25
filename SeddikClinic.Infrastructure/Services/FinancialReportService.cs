using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Core.Entities.Billing;
using SeddikClinic.Core.Entities.Financial;
using SeddikClinic.Core.Enums;
using SeddikClinic.Core.Interfaces;
using SeddikClinic.Infrastructure.Data;

namespace SeddikClinic.Infrastructure.Services;

public class FinancialReportService : IFinancialReportService
{
    private readonly SeddikClinicDbContext _dbContext;

    public FinancialReportService(SeddikClinicDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FinancialDashboardDto> GetDashboardMetricsAsync(FinancialFilterDto filter)
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var todayEnd = todayStart.AddDays(1).AddTicks(-1);

        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var currentMonthEnd = currentMonthStart.AddMonths(1).AddTicks(-1);

        var prevMonthStart = currentMonthStart.AddMonths(-1);
        var prevMonthEnd = currentMonthStart.AddTicks(-1);

        // حسابات اليوم
        var todayPaymentsQuery = _dbContext.PatientPayments.Where(p => p.PaymentDate >= todayStart && p.PaymentDate <= todayEnd);
        var todayExpensesQuery = _dbContext.Expenses.Where(e => e.PaymentDate >= todayStart && e.PaymentDate <= todayEnd && e.Status == ExpenseStatus.Paid);
        var todayRefundsQuery = _dbContext.PatientRefunds.Where(r => r.RefundDate >= todayStart && r.RefundDate <= todayEnd);

        if (filter.DoctorId.HasValue)
        {
            todayPaymentsQuery = todayPaymentsQuery.Where(p => p.DoctorId == filter.DoctorId.Value);
            todayExpensesQuery = todayExpensesQuery.Where(e => e.DoctorId == filter.DoctorId.Value);
            todayRefundsQuery = todayRefundsQuery.Where(r => r.DoctorId == filter.DoctorId.Value);
        }
        if (filter.BranchId.HasValue)
        {
            todayPaymentsQuery = todayPaymentsQuery.Where(p => p.BranchId == filter.BranchId.Value);
            todayExpensesQuery = todayExpensesQuery.Where(e => e.BranchId == filter.BranchId.Value);
            todayRefundsQuery = todayRefundsQuery.Where(r => r.BranchId == filter.BranchId.Value);
        }

        var todayRevenue = await todayPaymentsQuery.SumAsync(p => (decimal?)p.Amount) ?? 0m;
        var todayExpenses = await todayExpensesQuery.SumAsync(e => (decimal?)e.Amount) ?? 0m;
        var todayRefunds = await todayRefundsQuery.SumAsync(r => (decimal?)r.Amount) ?? 0m;
        var todayNetProfit = todayRevenue - todayRefunds - todayExpenses;

        // حسابات الشهر الحالي
        var monthPaymentsQuery = _dbContext.PatientPayments.Where(p => p.PaymentDate >= currentMonthStart && p.PaymentDate <= currentMonthEnd);
        var monthExpensesQuery = _dbContext.Expenses.Where(e => e.PaymentDate >= currentMonthStart && e.PaymentDate <= currentMonthEnd && e.Status == ExpenseStatus.Paid);
        var monthRefundsQuery = _dbContext.PatientRefunds.Where(r => r.RefundDate >= currentMonthStart && r.RefundDate <= currentMonthEnd);
        var monthInvoicesQuery = _dbContext.PatientInvoices.Where(i => i.InvoiceDate >= currentMonthStart && i.InvoiceDate <= currentMonthEnd);

        if (filter.DoctorId.HasValue)
        {
            monthPaymentsQuery = monthPaymentsQuery.Where(p => p.DoctorId == filter.DoctorId.Value);
            monthExpensesQuery = monthExpensesQuery.Where(e => e.DoctorId == filter.DoctorId.Value);
            monthRefundsQuery = monthRefundsQuery.Where(r => r.DoctorId == filter.DoctorId.Value);
            monthInvoicesQuery = monthInvoicesQuery.Where(i => i.DoctorId == filter.DoctorId.Value);
        }
        if (filter.BranchId.HasValue)
        {
            monthPaymentsQuery = monthPaymentsQuery.Where(p => p.BranchId == filter.BranchId.Value);
            monthExpensesQuery = monthExpensesQuery.Where(e => e.BranchId == filter.BranchId.Value);
            monthRefundsQuery = monthRefundsQuery.Where(r => r.BranchId == filter.BranchId.Value);
            monthInvoicesQuery = monthInvoicesQuery.Where(i => i.BranchId == filter.BranchId.Value);
        }

        var monthRevenue = await monthPaymentsQuery.SumAsync(p => (decimal?)p.Amount) ?? 0m;
        var monthExpenses = await monthExpensesQuery.SumAsync(e => (decimal?)e.Amount) ?? 0m;
        var monthRefunds = await monthRefundsQuery.SumAsync(r => (decimal?)r.Amount) ?? 0m;
        var monthNetProfit = monthRevenue - monthRefunds - monthExpenses;

        // الدفعات المقدمة والعربون
        var downPayments = await monthPaymentsQuery
            .Where(p => p.PaymentType == PaymentType.DownPayment || p.PaymentType == PaymentType.PartialPayment)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        // الذمم غير المحصلة (المبالغ المستحقة)
        var monthInvoices = await monthInvoicesQuery.ToListAsync();
        var totalUncollected = monthInvoices.Sum(i => i.RemainingAmount);

        // الربح التشغيلي التقديري = إيرادات الخدمات المنفذة - التكاليف المباشرة (معمل، مواد، مستلزمات)
        var directCostCategories = await _dbContext.ExpenseCategories
            .Where(c => c.IsDirectCost)
            .Select(c => c.Id)
            .ToListAsync();

        var directCostsSum = await _dbContext.Expenses
            .Where(e => e.PaymentDate >= currentMonthStart && e.PaymentDate <= currentMonthEnd &&
                        e.Status == ExpenseStatus.Paid && directCostCategories.Contains(e.CategoryId))
            .SumAsync(e => (decimal?)e.Amount) ?? 0m;

        var executedServicesRevenue = monthInvoices.Sum(i => i.TotalAmount);
        var estimatedOperatingProfit = executedServicesRevenue - directCostsSum;

        // مقارنة الشهر السابق
        var prevMonthPaymentsQuery = _dbContext.PatientPayments.Where(p => p.PaymentDate >= prevMonthStart && p.PaymentDate <= prevMonthEnd);
        var prevMonthExpensesQuery = _dbContext.Expenses.Where(e => e.PaymentDate >= prevMonthStart && e.PaymentDate <= prevMonthEnd && e.Status == ExpenseStatus.Paid);
        var prevMonthRefundsQuery = _dbContext.PatientRefunds.Where(r => r.RefundDate >= prevMonthStart && r.RefundDate <= prevMonthEnd);

        if (filter.DoctorId.HasValue)
        {
            prevMonthPaymentsQuery = prevMonthPaymentsQuery.Where(p => p.DoctorId == filter.DoctorId.Value);
            prevMonthExpensesQuery = prevMonthExpensesQuery.Where(e => e.DoctorId == filter.DoctorId.Value);
            prevMonthRefundsQuery = prevMonthRefundsQuery.Where(r => r.DoctorId == filter.DoctorId.Value);
        }

        var prevRevenue = await prevMonthPaymentsQuery.SumAsync(p => (decimal?)p.Amount) ?? 0m;
        var prevExpenses = await prevMonthExpensesQuery.SumAsync(e => (decimal?)e.Amount) ?? 0m;
        var prevRefunds = await prevMonthRefundsQuery.SumAsync(r => (decimal?)r.Amount) ?? 0m;
        var prevNetProfit = prevRevenue - prevRefunds - prevExpenses;

        var revenueGrowth = prevRevenue > 0 ? Math.Round(((monthRevenue - prevRevenue) / prevRevenue) * 100, 1) : 0m;
        var profitGrowth = prevNetProfit > 0 ? Math.Round(((monthNetProfit - prevNetProfit) / prevNetProfit) * 100, 1) : 0m;

        var daysPassedThisMonth = Math.Max(1, now.Day);
        var averageDailyIncome = Math.Round(monthRevenue / daysPassedThisMonth, 2);

        // أكثر الخدمات تحقيقاً للإيرادات
        var topServices = monthInvoices
            .Where(i => !string.IsNullOrEmpty(i.ServiceName))
            .GroupBy(i => i.ServiceName!)
            .Select(g => new TopServiceRevenueDto
            {
                ServiceName = g.Key,
                Count = g.Count(),
                TotalRevenue = g.Sum(x => x.PaidAmount),
                PercentageOfTotal = monthRevenue > 0 ? Math.Round((g.Sum(x => x.PaidAmount) / monthRevenue) * 100, 1) : 0
            })
            .OrderByDescending(s => s.TotalRevenue)
            .Take(5)
            .ToList();

        // أكثر أيام الشهر تحقيقاً للإيرادات
        var arabicCulture = new CultureInfo("ar-SA");
        var paymentsThisMonth = await monthPaymentsQuery.ToListAsync();
        var topDays = paymentsThisMonth
            .GroupBy(p => p.PaymentDate.Date)
            .Select(g => new TopEarningDayDto
            {
                Date = g.Key,
                DayNameAr = g.Key.ToString("dddd", arabicCulture),
                Revenue = g.Sum(x => x.Amount)
            })
            .OrderByDescending(d => d.Revenue)
            .Take(5)
            .ToList();

        // بيانات الرسم البياني اليومي للشهر الحالي
        var expensesThisMonth = await monthExpensesQuery.ToListAsync();
        var dailyPoints = new List<DailyFinancialPointDto>();

        for (int day = 1; day <= DateTime.DaysInMonth(now.Year, now.Month); day++)
        {
            var date = new DateTime(now.Year, now.Month, day, 0, 0, 0, DateTimeKind.Utc);
            var dayRev = paymentsThisMonth.Where(p => p.PaymentDate.Date == date.Date).Sum(p => p.Amount);
            var dayExp = expensesThisMonth.Where(e => e.PaymentDate.Date == date.Date).Sum(e => e.Amount);

            dailyPoints.Add(new DailyFinancialPointDto
            {
                Date = date,
                FormattedDate = $"{day}/{now.Month}",
                Revenue = dayRev,
                Expenses = dayExp
            });
        }

        // تفصيل المصروفات حسب التصنيف ومقارنتها بالميزانية
        var categories = await _dbContext.ExpenseCategories.Where(c => c.IsActive).ToListAsync();
        var budgets = await _dbContext.MonthlyBudgets
            .Where(b => b.Year == now.Year && b.Month == now.Month && (!filter.BranchId.HasValue || b.BranchId == filter.BranchId.Value))
            .ToListAsync();

        var categoryBreakdown = categories.Select(c =>
        {
            var spent = expensesThisMonth.Where(e => e.CategoryId == c.Id).Sum(e => e.Amount);
            var budget = budgets.FirstOrDefault(b => b.CategoryId == c.Id)?.BudgetAmount ?? 0m;
            return new CategoryExpenseBreakdownDto
            {
                CategoryId = c.Id,
                CategoryNameAr = c.NameAr,
                ColorHex = c.ColorHex,
                TotalAmount = spent,
                PercentageOfTotal = monthExpenses > 0 ? Math.Round((spent / monthExpenses) * 100, 1) : 0,
                BudgetAmount = budget
            };
        })
        .Where(x => x.TotalAmount > 0 || x.BudgetAmount > 0)
        .OrderByDescending(x => x.TotalAmount)
        .ToList();

        return new FinancialDashboardDto
        {
            TodayRevenue = todayRevenue,
            TodayExpenses = todayExpenses,
            TodayNetProfit = todayNetProfit,
            MonthRevenue = monthRevenue,
            MonthExpenses = monthExpenses,
            MonthNetProfit = monthNetProfit,
            TotalCollectedRevenue = monthRevenue,
            TotalUncollectedReceivables = totalUncollected,
            TotalDownPayments = downPayments,
            TotalRefunds = monthRefunds,
            NetCashFlow = monthNetProfit,
            EstimatedOperatingProfit = estimatedOperatingProfit,
            PreviousMonthRevenue = prevRevenue,
            PreviousMonthExpenses = prevExpenses,
            PreviousMonthNetProfit = prevNetProfit,
            RevenueGrowthPercentage = revenueGrowth,
            ProfitGrowthPercentage = profitGrowth,
            AverageDailyIncome = averageDailyIncome,
            TopRevenueServices = topServices,
            TopRevenueDays = topDays,
            DailyTrendChart = dailyPoints,
            CategoryExpenseBreakdown = categoryBreakdown
        };
    }

    public async Task<byte[]> ExportExpensesToExcelAsync(ExpenseFilterDto filter)
    {
        var expenses = await _dbContext.Expenses
            .Include(e => e.Category)
            .OrderByDescending(e => e.PaymentDate)
            .ToListAsync();

        var sb = new StringBuilder();
        // UTF-8 BOM for Arabic support in Excel
        sb.Append('\uFEFF');
        sb.AppendLine("رقم المصروف,اسم المصروف,التصنيف,القيمة,تاريخ الدفع,طريقة الدفع,الحالة,المستفيد,رقم الإيصال,ملاحظات");

        foreach (var item in expenses)
        {
            var status = item.Status switch
            {
                ExpenseStatus.Paid => "مدفوع",
                ExpenseStatus.Accrued => "مستحق",
                ExpenseStatus.Cancelled => "ملغي",
                ExpenseStatus.Refunded => "مسترد",
                _ => item.Status.ToString()
            };

            var method = item.PaymentMethod switch
            {
                ExpensePaymentMethod.Cash => "نقداً",
                ExpensePaymentMethod.DebitCreditCard => "بطاقة",
                ExpensePaymentMethod.BankTransfer => "تحويل بنكي",
                ExpensePaymentMethod.Cheque => "شيك",
                _ => item.PaymentMethod.ToString()
            };

            sb.AppendLine($"\"{item.ExpenseNumber}\",\"{item.Title}\",\"{item.Category?.NameAr}\",{item.Amount},\"{item.PaymentDate:yyyy-MM-dd}\",\"{method}\",\"{status}\",\"{item.BeneficiaryName}\",\"{item.ReceiptNumber}\",\"{item.Notes}\"");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> ExportExpensesToPdfAsync(ExpenseFilterDto filter)
    {
        // Simple and resilient HTML-to-PDF/printable template generator
        var expenses = await _dbContext.Expenses
            .Include(e => e.Category)
            .OrderByDescending(e => e.PaymentDate)
            .ToListAsync();

        var total = expenses.Where(e => e.Status == ExpenseStatus.Paid).Sum(e => e.Amount);

        var html = $@"
<!DOCTYPE html>
<html dir='rtl' lang='ar'>
<head>
<meta charset='utf-8'>
<title>تقرير المصروفات - عيادة الدكتور صديق</title>
<style>
body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; padding: 20px; }}
h1 {{ text-align: center; color: #1E3A8A; }}
table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
th, td {{ border: 1px solid #CBD5E1; padding: 8px 12px; text-align: right; }}
th {{ background-color: #F1F5F9; color: #0F172A; }}
.total {{ font-size: 1.2rem; font-weight: bold; margin-top: 20px; text-align: left; color: #047857; }}
</style>
</head>
<body>
<h1>تقرير المصروفات المالية للعيادة</h1>
<p>تاريخ التقرير: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</p>
<table>
<thead>
<tr>
<th>رقم المصروف</th>
<th>اسم المصروف</th>
<th>التصنيف</th>
<th>القيمة</th>
<th>تاريخ الدفع</th>
<th>طريقة الدفع</th>
<th>الحالة</th>
</tr>
</thead>
<tbody>
{string.Join("", expenses.Select(e => $"<tr><td>{e.ExpenseNumber}</td><td>{e.Title}</td><td>{e.Category?.NameAr}</td><td>{e.Amount:N2}</td><td>{e.PaymentDate:yyyy-MM-dd}</td><td>{e.PaymentMethod}</td><td>{e.Status}</td></tr>"))}
</tbody>
</table>
<div class='total'>إجمالي المصروفات المدفوعة: {total:N2}</div>
</body>
</html>";

        return Encoding.UTF8.GetBytes(html);
    }
}
