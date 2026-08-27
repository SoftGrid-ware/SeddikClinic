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
        var todayStart = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
        var todayEnd = todayStart.AddDays(1).AddTicks(-1);

        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var currentMonthEnd = currentMonthStart.AddMonths(1).AddTicks(-1);

        var prevMonthStart = currentMonthStart.AddMonths(-1);
        var prevMonthEnd = currentMonthStart.AddTicks(-1);

        // 1. تحديد نطاق الفترة المطلوبة بحسب الفلتر
        DateTime periodStart;
        DateTime periodEnd;

        if (filter.StartDate.HasValue && filter.EndDate.HasValue)
        {
            periodStart = DateTime.SpecifyKind(filter.StartDate.Value.Date, DateTimeKind.Utc);
            periodEnd = DateTime.SpecifyKind(filter.EndDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
        }
        else
        {
            switch (filter.PeriodType?.ToLower())
            {
                case "today":
                    periodStart = todayStart;
                    periodEnd = todayEnd;
                    break;
                case "week":
                    var diff = (7 + (int)now.DayOfWeek - (int)DayOfWeek.Saturday) % 7;
                    periodStart = DateTime.SpecifyKind(now.Date.AddDays(-diff), DateTimeKind.Utc);
                    periodEnd = periodStart.AddDays(7).AddTicks(-1);
                    break;
                case "year":
                    periodStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    periodEnd = periodStart.AddYears(1).AddTicks(-1);
                    break;
                default: // month
                    periodStart = currentMonthStart;
                    periodEnd = currentMonthEnd;
                    break;
            }
        }

        // =========================================================
        // حسابات اليوم (Today Metrics)
        // =========================================================
        var todayPaymentsQuery = _dbContext.PatientPayments.Where(p => p.PaymentDate >= todayStart && p.PaymentDate <= todayEnd);
        var todayExpensesQuery = _dbContext.Expenses.Where(e => e.PaymentDate >= todayStart && e.PaymentDate <= todayEnd && e.Status == ExpenseStatus.Paid);
        var todayRefundsQuery = _dbContext.PatientRefunds.Where(r => r.RefundDate >= todayStart && r.RefundDate <= todayEnd);
        var todayAptQuery = _dbContext.Appointments.Where(a => !a.IsDeleted && a.AppointmentDate.Date == todayStart.Date);

        if (filter.DoctorId.HasValue)
        {
            todayPaymentsQuery = todayPaymentsQuery.Where(p => p.DoctorId == filter.DoctorId.Value);
            todayExpensesQuery = todayExpensesQuery.Where(e => e.DoctorId == filter.DoctorId.Value);
            todayRefundsQuery = todayRefundsQuery.Where(r => r.DoctorId == filter.DoctorId.Value);
            todayAptQuery = todayAptQuery.Where(a => a.DoctorId == filter.DoctorId.Value);
        }
        if (filter.BranchId.HasValue)
        {
            todayPaymentsQuery = todayPaymentsQuery.Where(p => p.BranchId == filter.BranchId.Value);
            todayExpensesQuery = todayExpensesQuery.Where(e => e.BranchId == filter.BranchId.Value);
            todayRefundsQuery = todayRefundsQuery.Where(r => r.BranchId == filter.BranchId.Value);
            todayAptQuery = todayAptQuery.Where(a => a.BranchId == filter.BranchId.Value);
        }

        var todayApts = await todayAptQuery.ToListAsync();

        // ✅ إيراد اليوم = فقط ما اتحصل فعلاً من الحالات المنتهية
        // - كاش كامل (DepositAmount = 0 أو = TotalFees): يُحتسب TotalFees
        // - تقسيط (0 < DepositAmount < TotalFees): يُحتسب العربون المدفوع فقط
        var todayCompletedRevenue = todayApts
            .Where(a => a.Status == AppointmentStatus.Completed)
            .Sum(a => a.DepositAmount > 0 && a.DepositAmount < a.TotalFees
                ? a.DepositAmount   // تقسيط: فقط ما اتدفع
                : a.TotalFees);     // كاش: الإجمالي
        var todayRevenue = todayCompletedRevenue;
        var todayExpenses = await todayExpensesQuery.SumAsync(e => (decimal?)e.Amount) ?? 0m;
        var todayRefunds = await todayRefundsQuery.SumAsync(r => (decimal?)r.Amount) ?? 0m;
        var todayNetProfit = todayRevenue - todayRefunds - todayExpenses;

        // =========================================================
        // حسابات الفترة المحددة (Period Metrics - اليوم / الأسبوع / الشهر / السنة)
        // =========================================================
        var periodPaymentsQuery = _dbContext.PatientPayments.Where(p => p.PaymentDate >= periodStart && p.PaymentDate <= periodEnd);
        var periodExpensesQuery = _dbContext.Expenses.Where(e => e.PaymentDate >= periodStart && e.PaymentDate <= periodEnd && e.Status == ExpenseStatus.Paid);
        var periodRefundsQuery = _dbContext.PatientRefunds.Where(r => r.RefundDate >= periodStart && r.RefundDate <= periodEnd);
        var periodInvoicesQuery = _dbContext.PatientInvoices.Where(i => i.InvoiceDate >= periodStart && i.InvoiceDate <= periodEnd);
        var periodAptQuery = _dbContext.Appointments.Where(a => !a.IsDeleted && a.AppointmentDate.Date >= periodStart.Date && a.AppointmentDate.Date <= periodEnd.Date);

        if (filter.DoctorId.HasValue)
        {
            periodPaymentsQuery = periodPaymentsQuery.Where(p => p.DoctorId == filter.DoctorId.Value);
            periodExpensesQuery = periodExpensesQuery.Where(e => e.DoctorId == filter.DoctorId.Value);
            periodRefundsQuery = periodRefundsQuery.Where(r => r.DoctorId == filter.DoctorId.Value);
            periodInvoicesQuery = periodInvoicesQuery.Where(i => i.DoctorId == filter.DoctorId.Value);
            periodAptQuery = periodAptQuery.Where(a => a.DoctorId == filter.DoctorId.Value);
        }
        if (filter.BranchId.HasValue)
        {
            periodPaymentsQuery = periodPaymentsQuery.Where(p => p.BranchId == filter.BranchId.Value);
            periodExpensesQuery = periodExpensesQuery.Where(e => e.BranchId == filter.BranchId.Value);
            periodRefundsQuery = periodRefundsQuery.Where(r => r.BranchId == filter.BranchId.Value);
            periodInvoicesQuery = periodInvoicesQuery.Where(i => i.BranchId == filter.BranchId.Value);
            periodAptQuery = periodAptQuery.Where(a => a.BranchId == filter.BranchId.Value);
        }

        var periodApts = await periodAptQuery.ToListAsync();

        // ✅ إجمالي الإيرادات المحصلة:
        // - حجز مكتمل كاش (DepositAmount = TotalFees أو DepositAmount = 0): يُحتسب TotalFees كاملة
        // - حجز مكتمل بتقسيط (DepositAmount > 0 و < TotalFees): يُحتسب فقط ما اتدفع (DepositAmount)
        // + أقساط إضافية مسجلة في PatientPayments
        var completedAptRevenue = periodApts
            .Where(a => a.Status == AppointmentStatus.Completed)
            .Sum(a => a.DepositAmount > 0 && a.DepositAmount < a.TotalFees
                ? a.DepositAmount   // تقسيط: فقط العربون المدفوع
                : a.TotalFees);     // كاش كامل: الإجمالي

        // الأقساط الإضافية المسددة من جدول PatientPayments (دفعات مسجلة يدوياً)
        var additionalInstallments = await periodPaymentsQuery
            .Where(p => p.PaymentType == PaymentType.PartialPayment)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        // الإيراد = ما اتحصل فعلاً من المنتهية + أقساط مسجلة
        var periodRevenue = completedAptRevenue + additionalInstallments;

        var periodExpenses = await periodExpensesQuery.SumAsync(e => (decimal?)e.Amount) ?? 0m;
        var periodRefunds = await periodRefundsQuery.SumAsync(r => (decimal?)r.Amount) ?? 0m;
        var periodNetProfit = periodRevenue - periodRefunds - periodExpenses;

        // ✅ العربون والدفعات الجزئية:
        // = فقط من دفع عربون جزئي (ليس كاش كامل) — سواء حجز نشط أو منتهي
        // + أي أقساط إضافية مسددة
        var downPayments = periodApts
            .Where(a => a.Status != AppointmentStatus.Cancelled
                     && a.DepositAmount > 0
                     && a.DepositAmount < a.TotalFees) // عربون جزئي فعلي فقط
            .Sum(a => a.DepositAmount)
            + additionalInstallments;

        // ✅ المبالغ المستحقة الغير محصلة:
        // = الحالات النشطة (لم تنته): المتبقي = TotalFees - DepositAmount
        // + الحالات المنتهية بتقسيط: المتبقي من الإجمالي بعد خصم العربون
        var periodInvoices = await periodInvoicesQuery.ToListAsync();
        var invoiceUncollected = periodInvoices.Sum(i => i.RemainingAmount);

        var aptUncollected = periodApts
            .Where(a => a.Status != AppointmentStatus.Cancelled
                     && (a.TotalFees - a.DepositAmount) > 0) // أي حجز فيه متبقي لم يُحصَّل
            .Sum(a => Math.Max(0, a.TotalFees - a.DepositAmount));

        var totalUncollected = invoiceUncollected + aptUncollected;

        // الربح التشغيلي التقديري = الإيرادات المحصلة - التكاليف المباشرة
        var directCostCategories = await _dbContext.ExpenseCategories
            .Where(c => c.IsDirectCost)
            .Select(c => c.Id)
            .ToListAsync();

        var directCostsSum = await _dbContext.Expenses
            .Where(e => e.PaymentDate >= periodStart && e.PaymentDate <= periodEnd &&
                        e.Status == ExpenseStatus.Paid && directCostCategories.Contains(e.CategoryId))
            .SumAsync(e => (decimal?)e.Amount) ?? 0m;

        var estimatedOperatingProfit = periodRevenue - directCostsSum;

        // مقارنة الشهر السابق
        var prevMonthPaymentsQuery = _dbContext.PatientPayments.Where(p => p.PaymentDate >= prevMonthStart && p.PaymentDate <= prevMonthEnd);
        var prevMonthExpensesQuery = _dbContext.Expenses.Where(e => e.PaymentDate >= prevMonthStart && e.PaymentDate <= prevMonthEnd && e.Status == ExpenseStatus.Paid);
        var prevMonthRefundsQuery = _dbContext.PatientRefunds.Where(r => r.RefundDate >= prevMonthStart && r.RefundDate <= prevMonthEnd);
        var prevAptQuery = _dbContext.Appointments.Where(a => !a.IsDeleted && a.AppointmentDate.Date >= prevMonthStart.Date && a.AppointmentDate.Date <= prevMonthEnd.Date && a.Status == AppointmentStatus.Completed);

        if (filter.DoctorId.HasValue)
        {
            prevMonthPaymentsQuery = prevMonthPaymentsQuery.Where(p => p.DoctorId == filter.DoctorId.Value);
            prevMonthExpensesQuery = prevMonthExpensesQuery.Where(e => e.DoctorId == filter.DoctorId.Value);
            prevMonthRefundsQuery = prevMonthRefundsQuery.Where(r => r.DoctorId == filter.DoctorId.Value);
            prevAptQuery = prevAptQuery.Where(a => a.DoctorId == filter.DoctorId.Value);
        }

        var prevRevenue = (await prevMonthPaymentsQuery.SumAsync(p => (decimal?)p.Amount) ?? 0m) + (await prevAptQuery.SumAsync(a => (decimal?)a.TotalFees) ?? 0m);
        var prevExpenses = await prevMonthExpensesQuery.SumAsync(e => (decimal?)e.Amount) ?? 0m;
        var prevRefunds = await prevMonthRefundsQuery.SumAsync(r => (decimal?)r.Amount) ?? 0m;
        var prevNetProfit = prevRevenue - prevRefunds - prevExpenses;

        var revenueGrowth = prevRevenue > 0 ? Math.Round(((periodRevenue - prevRevenue) / prevRevenue) * 100, 1) : 0m;
        var profitGrowth = prevNetProfit > 0 ? Math.Round(((periodNetProfit - prevNetProfit) / prevNetProfit) * 100, 1) : 0m;

        var daysPassed = Math.Max(1, (periodEnd.Date - periodStart.Date).Days + 1);
        var averageDailyIncome = Math.Round(periodRevenue / daysPassed, 2);

        // أكثر الخدمات تحقيقاً للإيرادات (من الحجوزات المكتملة والفواتير)
        var servicesList = new List<TopServiceRevenueDto>();

        foreach (var apt in periodApts.Where(a => a.Status == AppointmentStatus.Completed && !string.IsNullOrWhiteSpace(a.ServiceType)))
        {
            var parts = apt.ServiceType.Split(new[] { " + ", "+", "،", "," }, StringSplitOptions.RemoveEmptyEntries);
            var feesPerService = parts.Length > 0 ? apt.TotalFees / parts.Length : apt.TotalFees;
            foreach (var part in parts)
            {
                var cleanName = part.Trim();
                servicesList.Add(new TopServiceRevenueDto
                {
                    ServiceName = cleanName,
                    Count = 1,
                    TotalRevenue = feesPerService
                });
            }
        }

        var invoiceServices = periodInvoices
            .Where(i => !string.IsNullOrEmpty(i.ServiceName))
            .GroupBy(i => i.ServiceName!)
            .Select(g => new TopServiceRevenueDto
            {
                ServiceName = g.Key,
                Count = g.Count(),
                TotalRevenue = g.Sum(x => x.PaidAmount),
                PercentageOfTotal = periodRevenue > 0 ? Math.Round((g.Sum(x => x.PaidAmount) / periodRevenue) * 100, 1) : 0
            });
        servicesList.AddRange(invoiceServices);

        var topServices = servicesList
            .GroupBy(s => s.ServiceName)
            .Select(g => new TopServiceRevenueDto
            {
                ServiceName = g.Key,
                Count = g.Sum(x => x.Count),
                TotalRevenue = g.Sum(x => x.TotalRevenue),
                PercentageOfTotal = periodRevenue > 0 ? Math.Round((g.Sum(x => x.TotalRevenue) / periodRevenue) * 100, 1) : 0
            })
            .OrderByDescending(s => s.TotalRevenue)
            .Take(5)
            .ToList();

        // أكثر أيام الشهر تحقيقاً للإيرادات
        var arabicCulture = new CultureInfo("ar-SA");
        var paymentsThisMonth = await periodPaymentsQuery.ToListAsync();

        var daysRevenueMap = new Dictionary<DateTime, decimal>();
        foreach (var p in paymentsThisMonth)
        {
            var d = p.PaymentDate.Date;
            daysRevenueMap[d] = daysRevenueMap.GetValueOrDefault(d, 0m) + p.Amount;
        }
        foreach (var a in periodApts.Where(a => a.Status == AppointmentStatus.Completed))
        {
            var d = a.AppointmentDate.Date;
            daysRevenueMap[d] = daysRevenueMap.GetValueOrDefault(d, 0m) + a.TotalFees;
        }

        var topDays = daysRevenueMap
            .Select(kvp => new TopEarningDayDto
            {
                Date = kvp.Key,
                DayNameAr = kvp.Key.ToString("dddd", arabicCulture),
                Revenue = kvp.Value
            })
            .OrderByDescending(d => d.Revenue)
            .Take(5)
            .ToList();

        // بيانات الرسم البياني اليومي للشهر الحالي
        var expensesThisMonth = await periodExpensesQuery.ToListAsync();
        var dailyPoints = new List<DailyFinancialPointDto>();

        // جلب حجوزات الشهر بالكامل للرسم البياني
        var monthApts = await _dbContext.Appointments
            .Where(a => !a.IsDeleted && a.AppointmentDate.Date >= currentMonthStart.Date && a.AppointmentDate.Date <= currentMonthEnd.Date && a.Status == AppointmentStatus.Completed)
            .ToListAsync();

        for (int day = 1; day <= DateTime.DaysInMonth(now.Year, now.Month); day++)
        {
            var date = new DateTime(now.Year, now.Month, day, 0, 0, 0, DateTimeKind.Utc);
            var dayPaymentsRev = paymentsThisMonth.Where(p => p.PaymentDate.Date == date.Date).Sum(p => p.Amount);
            var dayAptRev = monthApts.Where(a => a.AppointmentDate.Date == date.Date).Sum(a => a.TotalFees);
            var dayExp = expensesThisMonth.Where(e => e.PaymentDate.Date == date.Date).Sum(e => e.Amount);

            dailyPoints.Add(new DailyFinancialPointDto
            {
                Date = date,
                FormattedDate = $"{day}/{now.Month}",
                Revenue = dayPaymentsRev + dayAptRev,
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
                PercentageOfTotal = periodExpenses > 0 ? Math.Round((spent / periodExpenses) * 100, 1) : 0,
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
            MonthRevenue = periodRevenue,
            MonthExpenses = periodExpenses,
            MonthNetProfit = periodNetProfit,
            TotalCollectedRevenue = periodRevenue,
            TotalUncollectedReceivables = totalUncollected,
            TotalDownPayments = downPayments,
            TotalRefunds = periodRefunds,
            NetCashFlow = periodNetProfit,
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
