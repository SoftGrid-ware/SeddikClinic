using Moq;
using Microsoft.EntityFrameworkCore;
using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Core.Entities.Financial;
using SeddikClinic.Core.Enums;
using SeddikClinic.Core.Interfaces;
using SeddikClinic.Infrastructure.Data;
using SeddikClinic.Infrastructure.Services;
using Xunit;

namespace SeddikClinic.Tests;

public class ExpenseServiceTests
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
    public async Task CreateExpense_ThrowsException_WhenPeriodIsClosed()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockStorage = new Mock<IFileStorageService>();
        var auditService = new AuditLogService(context);
        var periodService = new FinancialPeriodService(context, auditService);
        var expenseService = new ExpenseService(context, periodService, auditService, mockStorage.Object);

        var branchId = Guid.NewGuid();
        var paymentDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);

        // إقفال الفترة المالية لشهر 8
        var periodDto = await periodService.GetOrCreateCurrentPeriodAsync(branchId, 2026, 8);
        await periodService.ClosePeriodAsync(new ClosePeriodDto { PeriodId = periodDto.Id }, "DOC_01", "د. صديق", null, null);

        var category = await context.ExpenseCategories.FirstAsync();
        var createDto = new CreateExpenseDto
        {
            Title = "فاتورة صيانة جهاز",
            CategoryId = category.Id,
            Amount = 1500m,
            PaymentDate = paymentDate,
            PaymentMethod = ExpensePaymentMethod.Cash,
            Status = ExpenseStatus.Paid,
            BranchId = branchId
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            expenseService.CreateExpenseAsync(createDto, "DOC_01", "د. صديق", null, null));
    }

    [Fact]
    public async Task CreateAndCancelExpense_MaintainsAuditLogAndSoftDelete()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockStorage = new Mock<IFileStorageService>();
        var auditService = new AuditLogService(context);
        var periodService = new FinancialPeriodService(context, auditService);
        var expenseService = new ExpenseService(context, periodService, auditService, mockStorage.Object);

        var branchId = Guid.NewGuid();
        var category = await context.ExpenseCategories.FirstAsync();

        var createDto = new CreateExpenseDto
        {
            Title = "فاتورة كهرباء العيادة",
            CategoryId = category.Id,
            Amount = 450m,
            PaymentDate = DateTime.UtcNow,
            PaymentMethod = ExpensePaymentMethod.BankTransfer,
            Status = ExpenseStatus.Paid,
            BranchId = branchId
        };

        // Act 1: Create
        var created = await expenseService.CreateExpenseAsync(createDto, "DOC_01", "د. صديق", "127.0.0.1", "WindowsApp");
        Assert.NotNull(created);
        Assert.StartsWith("EXP-", created.ExpenseNumber);

        // Act 2: Cancel
        var cancelled = await expenseService.CancelExpenseAsync(created.Id, "تم تسجيل الفاتورة بالخطأ", "DOC_01", "د. صديق", "127.0.0.1", "WindowsApp");
        Assert.True(cancelled);

        // Assert
        var fetched = await expenseService.GetExpenseByIdAsync(created.Id);
        Assert.NotNull(fetched);
        Assert.Equal(ExpenseStatus.Cancelled, fetched.Status);

        // التحقق من وجود حركات في سجل التدقيق Audit Log
        var logs = await auditService.GetLogsForRecordAsync("Expense", created.Id.ToString());
        Assert.NotEmpty(logs);
        Assert.Contains(logs, l => l.ActionTypeNameAr == "إنشاء");
        Assert.Contains(logs, l => l.ActionTypeNameAr == "إلغاء");
    }

    [Fact]
    public async Task RecurringExpense_ProcessesSuccessfully()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockStorage = new Mock<IFileStorageService>();
        var auditService = new AuditLogService(context);
        var periodService = new FinancialPeriodService(context, auditService);
        var expenseService = new ExpenseService(context, periodService, auditService, mockStorage.Object);

        var branchId = Guid.NewGuid();
        var category = await context.ExpenseCategories.FirstAsync();

        // إنشاء مصروف شهري متكرر (إيجار العيادة)
        var recurringDto = new CreateRecurringExpenseDto
        {
            Title = "إيجار العيادة الشهري",
            CategoryId = category.Id,
            Amount = 6000m,
            DayOfMonth = DateTime.UtcNow.Day, // مستحق اليوم
            StartDate = DateTime.UtcNow.AddMonths(-1),
            AutoCreate = true,
            BranchId = branchId,
            BeneficiaryName = "مالك العقار"
        };

        await expenseService.CreateRecurringExpenseAsync(recurringDto, "DOC_01");

        // Act: Process
        var generatedCount = await expenseService.ProcessDueRecurringExpensesAsync();

        // Assert
        Assert.Equal(1, generatedCount);

        var expenses = await expenseService.GetExpensesAsync(new ExpenseFilterDto { BranchId = branchId });
        Assert.Contains(expenses, e => e.Title == "إيجار العيادة الشهري" && e.Status == ExpenseStatus.Accrued);
    }
}
