using Microsoft.EntityFrameworkCore;
using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Core.Entities.Billing;
using SeddikClinic.Core.Entities.Financial;
using SeddikClinic.Core.Enums;
using SeddikClinic.Infrastructure.Data;
using SeddikClinic.Infrastructure.Services;
using Xunit;

namespace SeddikClinic.Tests;

public class FinancialReportServiceTests
{
    private SeddikClinicDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SeddikClinicDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new SeddikClinicDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task DashboardMetrics_CalculatesTotalRevenueAndCashFlowAccurately()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var reportService = new FinancialReportService(context);

        var branchId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // 1. فاتورة علاج بقيمة 5000، سدد منها المريض 3000 فقط نقداً
        var invoice = new PatientInvoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-001",
            PatientId = Guid.NewGuid(),
            DoctorId = doctorId,
            BranchId = branchId,
            InvoiceDate = now,
            TotalAmount = 5000m,
            PaidAmount = 3000m,
            ServiceName = "زراعة أسنان"
        };
        context.PatientInvoices.Add(invoice);

        // دفعة فعلية محصلة = 3000
        var payment = new PatientPayment
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = "REC-001",
            InvoiceId = invoice.Id,
            DoctorId = doctorId,
            BranchId = branchId,
            Amount = 3000m,
            PaymentDate = now,
            PaymentType = PaymentType.DownPayment
        };
        context.PatientPayments.Add(payment);

        // 2. استرداد مبلغ = 500
        var refund = new PatientRefund
        {
            Id = Guid.NewGuid(),
            RefundNumber = "REF-001",
            InvoiceId = invoice.Id,
            DoctorId = doctorId,
            BranchId = branchId,
            Amount = 500m,
            RefundDate = now,
            Reason = "تعديل خطة العلاج"
        };
        context.PatientRefunds.Add(refund);

        // 3. مصروف مدفوع (مستلزمات معمل أسنان - تكلفة مباشرة) = 800
        var labCategory = await context.ExpenseCategories.FirstAsync(c => c.IsDirectCost);
        var paidExpense = new Expense
        {
            Id = Guid.NewGuid(),
            ExpenseNumber = "EXP-001",
            Title = "شراء مواد حشو",
            CategoryId = labCategory.Id,
            Amount = 800m,
            PaymentDate = now,
            Status = ExpenseStatus.Paid,
            BranchId = branchId,
            DoctorId = doctorId
        };
        context.Expenses.Add(paidExpense);

        // 4. مصروف مستحق غير مدفوع = 1200 (لا يجب أن يخصم من التدفق النقدي حتى يسدد)
        var accruedExpense = new Expense
        {
            Id = Guid.NewGuid(),
            ExpenseNumber = "EXP-002",
            Title = "فاتورة صيانة مستحقة",
            CategoryId = labCategory.Id,
            Amount = 1200m,
            PaymentDate = now,
            Status = ExpenseStatus.Accrued,
            BranchId = branchId,
            DoctorId = doctorId
        };
        context.Expenses.Add(accruedExpense);

        await context.SaveChangesAsync();

        // Act
        var filter = new FinancialFilterDto { BranchId = branchId };
        var dashboard = await reportService.GetDashboardMetricsAsync(filter);

        // Assert
        // الإيرادات المحصلة فعلياً = 3000 (وليس 5000 قيمة الفاتورة)
        Assert.Equal(3000m, dashboard.MonthRevenue);

        // المصروفات المدفوعة = 800 (المصروف المستحق 1200 لا يحسب هنا)
        Assert.Equal(800m, dashboard.MonthExpenses);

        // صافي التدفق النقدي = 3000 (محصل) - 500 (مسترد) - 800 (مصروف مدفوع) = 1700
        Assert.Equal(1700m, dashboard.NetCashFlow);

        // الذمم غير المحصلة = 5000 - 3000 = 2000
        Assert.Equal(2000m, dashboard.TotalUncollectedReceivables);

        // العربون / الدفعات الجزئية = 3000
        Assert.Equal(3000m, dashboard.TotalDownPayments);

        // الربح التشغيلي التقديري = 5000 (إيراد الخدمة المنفذة) - 800 (تكلفة مباشرة) = 4200
        Assert.Equal(4200m, dashboard.EstimatedOperatingProfit);
    }
}
