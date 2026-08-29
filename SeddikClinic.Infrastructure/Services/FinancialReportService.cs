using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Core.Entities.Appointments;
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

    private static decimal GetAppointmentCollectedAmount(Appointment a)
    {
        if (a.Status == AppointmentStatus.Cancelled) return 0m;

        // الإيراد المحصل = فقط وحصرياً ما تم تحصيله وسداده كاش/عربون فعلياً
        var netFees = Math.Max(0, a.TotalFees - a.DiscountAmount);
        if (a.DepositAmount > 0)
        {
            return Math.Min(a.DepositAmount, netFees);
        }

        // إذا كان المحصل = 0، فالإيراد = 0 تماماً ويذهب بالكامل إلى المبالغ المستحقة غير المحصلة (المتبقي)
        return 0m;
    }

    private static decimal GetAppointmentRemainingAmount(Appointment a)
    {
        if (a.Status == AppointmentStatus.Cancelled) return 0m;
        var netFees = Math.Max(0, a.TotalFees - a.DiscountAmount);
        var collected = GetAppointmentCollectedAmount(a);
        return Math.Max(0, netFees - collected);
    }

    public async Task<FinancialDashboardDto> GetDashboardMetricsAsync(FinancialFilterDto filter)
    {
        var localToday = DateTime.Today;
        var todayStart = DateTime.SpecifyKind(localToday, DateTimeKind.Utc);
        var todayEnd = todayStart.AddDays(1);

        var currentMonthStart = new DateTime(localToday.Year, localToday.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var currentMonthEnd = currentMonthStart.AddMonths(1);

        var prevMonthStart = currentMonthStart.AddMonths(-1);
        var prevMonthEnd = currentMonthStart;

        // 1. تحديد نطاق الفترة المطلوبة بحسب الفلتر
        DateTime periodStart;
        DateTime periodEnd;

        if (filter.StartDate.HasValue && filter.EndDate.HasValue)
        {
            periodStart = DateTime.SpecifyKind(filter.StartDate.Value.Date, DateTimeKind.Utc);
            periodEnd = DateTime.SpecifyKind(filter.EndDate.Value.Date.AddDays(1), DateTimeKind.Utc);
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
                    var diff = (7 + (int)localToday.DayOfWeek - (int)DayOfWeek.Saturday) % 7;
                    periodStart = DateTime.SpecifyKind(localToday.AddDays(-diff), DateTimeKind.Utc);
                    periodEnd = periodStart.AddDays(7);
                    break;
                case "year":
                    periodStart = new DateTime(localToday.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    periodEnd = periodStart.AddYears(1);
                    break;
                default: // month
                    periodStart = currentMonthStart;
                    periodEnd = currentMonthEnd;
                    break;
            }
        }

        // =========================================================
        // حسابات اليوم (Today Metrics - المقبوضات والمصروفات النقدية لليوم)
        // =========================================================
        var todayPaymentsQuery = _dbContext.PatientPayments.Where(p => p.PaymentDate >= todayStart && p.PaymentDate < todayEnd);
        var todayExpensesQuery = _dbContext.Expenses.Where(e => e.PaymentDate >= todayStart && e.PaymentDate < todayEnd && e.Status == ExpenseStatus.Paid);
        var todayRefundsQuery = _dbContext.PatientRefunds.Where(r => r.RefundDate >= todayStart && r.RefundDate < todayEnd);
        var todayAptQuery = _dbContext.Appointments.Where(a => !a.IsDeleted && a.AppointmentDate >= todayStart && a.AppointmentDate < todayEnd);

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
        var todayPayments = await todayPaymentsQuery.ToListAsync();

        // إيراد وتحصيل اليوم = كاش وعربون حجوزات اليوم + أي أقساط تم سدادها اليوم
        var todayRevenue = todayApts.Sum(GetAppointmentCollectedAmount) + todayPayments.Sum(p => p.Amount);
        var todayExpenses = await todayExpensesQuery.SumAsync(e => (decimal?)e.Amount) ?? 0m;
        var todayRefunds = await todayRefundsQuery.SumAsync(r => (decimal?)r.Amount) ?? 0m;
        var todayNetProfit = todayRevenue - todayRefunds - todayExpenses;

        // =========================================================
        // حسابات الفترة المحددة (Period Metrics - اليوم / الأسبوع / الشهر / السنة)
        // =========================================================
        var periodPaymentsQuery = _dbContext.PatientPayments.Where(p => p.PaymentDate >= periodStart && p.PaymentDate < periodEnd);
        var periodExpensesQuery = _dbContext.Expenses.Where(e => e.PaymentDate >= periodStart && e.PaymentDate < periodEnd && e.Status == ExpenseStatus.Paid);
        var periodRefundsQuery = _dbContext.PatientRefunds.Where(r => r.RefundDate >= periodStart && r.RefundDate < periodEnd);
        var periodInvoicesQuery = _dbContext.PatientInvoices.Where(i => i.InvoiceDate >= periodStart && i.InvoiceDate < periodEnd);
        var periodAptQuery = _dbContext.Appointments.Where(a => !a.IsDeleted && a.AppointmentDate >= periodStart && a.AppointmentDate < periodEnd);

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
        var periodPayments = await periodPaymentsQuery.ToListAsync();
        var periodInvoices = await periodInvoicesQuery.ToListAsync();

        // ✅ إجمالي الإيرادات المحصلة نقداً (Actual Cash Collections)
        var aptsCollectedRevenue = periodApts.Sum(GetAppointmentCollectedAmount);
        var paymentsCollectedRevenue = periodPayments.Sum(p => p.Amount);

        // الإيراد المحصل = مجموع ما دخل الصندوق فعلياً (المواعيد المسددة + الدفعات وفواتير المرضى المسجلة في السندات)
        var periodRevenue = aptsCollectedRevenue + paymentsCollectedRevenue;

        var periodExpenses = await periodExpensesQuery.SumAsync(e => (decimal?)e.Amount) ?? 0m;
        var periodRefunds = await periodRefundsQuery.SumAsync(r => (decimal?)r.Amount) ?? 0m;
        var periodNetProfit = periodRevenue - periodRefunds - periodExpenses;

        // ✅ العربون والدفعات الجزئية (Down Payments & Installments Collected):
        // = مجموع العربين والدفعات المسددة لحالات التقسيط والدفعات الجزئية
        var downPayments = periodApts
            .Where(a => a.Status != AppointmentStatus.Cancelled && a.DepositAmount > 0)
            .Sum(a => Math.Min(a.DepositAmount, Math.Max(0, a.TotalFees - a.DiscountAmount)))
            + periodPayments.Where(p => p.PaymentType == PaymentType.DownPayment || p.PaymentType == PaymentType.PartialPayment).Sum(p => p.Amount);

        // ✅ المبالغ المستحقة غير المحصلة (Accounts Receivable / الذمم المدينة):
        // = المتبقي من إجمالي قيمة الكشوفات والفواتير ولم يتم تحصيله بعد
        var invoiceUncollected = periodInvoices.Sum(i => i.RemainingAmount);
        var aptUncollected = periodApts.Sum(GetAppointmentRemainingAmount);
        var totalUncollected = invoiceUncollected + aptUncollected;

        // ✅ الربح التشغيلي التقديري:
        // = قيمة الخدمات الإجمالية (أو المحصلة) مطروحاً منها التكاليف المباشرة للمعمل والمستلزمات
        var directCostCategories = await _dbContext.ExpenseCategories
            .Where(c => c.IsDirectCost)
            .Select(c => c.Id)
            .ToListAsync();

        var directCostsSum = await _dbContext.Expenses
            .Where(e => e.PaymentDate >= periodStart && e.PaymentDate < periodEnd &&
                        e.Status == ExpenseStatus.Paid && directCostCategories.Contains(e.CategoryId))
            .SumAsync(e => (decimal?)e.Amount) ?? 0m;

        var totalTreatmentValue = periodApts.Where(a => a.Status != AppointmentStatus.Cancelled).Sum(a => a.TotalFees)
                                  + periodInvoices.Sum(i => i.TotalAmount);

        var estimatedOperatingProfit = (totalTreatmentValue > 0 ? totalTreatmentValue : periodRevenue) - directCostsSum;

        // مقارنة الشهر السابق
        var prevMonthPaymentsQuery = _dbContext.PatientPayments.Where(p => p.PaymentDate >= prevMonthStart && p.PaymentDate < prevMonthEnd);
        var prevMonthExpensesQuery = _dbContext.Expenses.Where(e => e.PaymentDate >= prevMonthStart && e.PaymentDate < prevMonthEnd && e.Status == ExpenseStatus.Paid);
        var prevMonthRefundsQuery = _dbContext.PatientRefunds.Where(r => r.RefundDate >= prevMonthStart && r.RefundDate < prevMonthEnd);
        var prevAptQuery = _dbContext.Appointments.Where(a => !a.IsDeleted && a.AppointmentDate >= prevMonthStart && a.AppointmentDate < prevMonthEnd);

        if (filter.DoctorId.HasValue)
        {
            prevMonthPaymentsQuery = prevMonthPaymentsQuery.Where(p => p.DoctorId == filter.DoctorId.Value);
            prevMonthExpensesQuery = prevMonthExpensesQuery.Where(e => e.DoctorId == filter.DoctorId.Value);
            prevMonthRefundsQuery = prevMonthRefundsQuery.Where(r => r.DoctorId == filter.DoctorId.Value);
            prevAptQuery = prevAptQuery.Where(a => a.DoctorId == filter.DoctorId.Value);
        }

        var prevAptsList = await prevAptQuery.ToListAsync();
        var prevRevenue = (await prevMonthPaymentsQuery.SumAsync(p => (decimal?)p.Amount) ?? 0m) + prevAptsList.Sum(GetAppointmentCollectedAmount);
        var prevExpenses = await prevMonthExpensesQuery.SumAsync(e => (decimal?)e.Amount) ?? 0m;
        var prevRefunds = await prevMonthRefundsQuery.SumAsync(r => (decimal?)r.Amount) ?? 0m;
        var prevNetProfit = prevRevenue - prevRefunds - prevExpenses;

        var revenueGrowth = prevRevenue > 0 ? Math.Round(((periodRevenue - prevRevenue) / prevRevenue) * 100, 1) : 0m;
        var profitGrowth = prevNetProfit > 0 ? Math.Round(((periodNetProfit - prevNetProfit) / prevNetProfit) * 100, 1) : 0m;

        var daysPassed = Math.Max(1, (periodEnd.Date - periodStart.Date).Days);
        var averageDailyIncome = Math.Round(periodRevenue / daysPassed, 2);

        // أكثر الخدمات تحقيقاً للإيرادات
        var servicesList = new List<TopServiceRevenueDto>();

        foreach (var apt in periodApts.Where(a => a.Status != AppointmentStatus.Cancelled && !string.IsNullOrWhiteSpace(a.ServiceType)))
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

        var daysRevenueMap = new Dictionary<DateTime, decimal>();
        foreach (var p in periodPayments)
        {
            var d = p.PaymentDate.Date;
            daysRevenueMap[d] = daysRevenueMap.GetValueOrDefault(d, 0m) + p.Amount;
        }
        foreach (var a in periodApts.Where(a => a.Status != AppointmentStatus.Cancelled))
        {
            var d = a.AppointmentDate.Date;
            daysRevenueMap[d] = daysRevenueMap.GetValueOrDefault(d, 0m) + GetAppointmentCollectedAmount(a);
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
        var dailyPoints = new List<DailyFinancialPointDto>();
        var periodExpensesList = await periodExpensesQuery.ToListAsync();

        for (int day = 1; day <= DateTime.DaysInMonth(currentMonthStart.Year, currentMonthStart.Month); day++)
        {
            var dayStart = new DateTime(currentMonthStart.Year, currentMonthStart.Month, day, 0, 0, 0, DateTimeKind.Utc);
            var dayEnd = dayStart.AddDays(1);

            var dayPaymentsRev = periodPayments.Where(p => p.PaymentDate >= dayStart && p.PaymentDate < dayEnd).Sum(p => p.Amount);
            var dayAptRev = periodApts.Where(a => a.AppointmentDate >= dayStart && a.AppointmentDate < dayEnd).Sum(GetAppointmentCollectedAmount);
            var dayExp = periodExpensesList.Where(e => e.PaymentDate >= dayStart && e.PaymentDate < dayEnd).Sum(e => e.Amount);

            dailyPoints.Add(new DailyFinancialPointDto
            {
                Date = dayStart,
                FormattedDate = $"{day}/{currentMonthStart.Month}",
                Revenue = dayPaymentsRev + dayAptRev,
                Expenses = dayExp
            });
        }

        // تفصيل المصروفات حسب التصنيف ومقارنتها بالميزانية
        var categories = await _dbContext.ExpenseCategories.Where(c => c.IsActive).ToListAsync();
        var budgets = await _dbContext.MonthlyBudgets
            .Where(b => b.Year == localToday.Year && b.Month == localToday.Month && (!filter.BranchId.HasValue || b.BranchId == filter.BranchId.Value))
            .ToListAsync();

        var categoryBreakdown = categories.Select(c =>
        {
            var spent = periodExpensesList.Where(e => e.CategoryId == c.Id).Sum(e => e.Amount);
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
