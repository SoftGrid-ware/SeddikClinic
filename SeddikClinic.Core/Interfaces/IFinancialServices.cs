using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Core.Entities.Financial;

namespace SeddikClinic.Core.Interfaces;

public interface IFinancialReportService
{
    Task<FinancialDashboardDto> GetDashboardMetricsAsync(FinancialFilterDto filter);
    Task<byte[]> ExportExpensesToPdfAsync(ExpenseFilterDto filter);
    Task<byte[]> ExportExpensesToExcelAsync(ExpenseFilterDto filter);
}

public interface IExpenseService
{
    Task<IEnumerable<ExpenseDto>> GetExpensesAsync(ExpenseFilterDto filter);
    Task<int> GetExpensesCountAsync(ExpenseFilterDto filter);
    Task<ExpenseDto?> GetExpenseByIdAsync(Guid id);
    Task<ExpenseDto> CreateExpenseAsync(CreateExpenseDto dto, string userId, string userName, string? ipAddress, string? deviceInfo);
    Task<ExpenseDto> UpdateExpenseAsync(Guid id, UpdateExpenseDto dto, string userId, string userName, string? ipAddress, string? deviceInfo);
    Task<bool> CancelExpenseAsync(Guid id, string reason, string userId, string userName, string? ipAddress, string? deviceInfo);
    Task<ExpenseAttachmentDto> AddAttachmentAsync(Guid expenseId, Stream stream, string fileName, string contentType, string userId);
    Task<IEnumerable<ExpenseCategoryDto>> GetCategoriesAsync();
    
    // المصروفات الدورية
    Task<IEnumerable<RecurringExpenseDto>> GetRecurringExpensesAsync(Guid? branchId);
    Task<RecurringExpenseDto> CreateRecurringExpenseAsync(CreateRecurringExpenseDto dto, string userId);
    Task<bool> ToggleRecurringExpenseAsync(Guid id, bool isActive);
    Task<int> ProcessDueRecurringExpensesAsync(); // يتم استدعاؤه من Background Worker دورياً

    // الموازنات الشهرية
    Task<IEnumerable<MonthlyBudgetDto>> GetMonthlyBudgetsAsync(int year, int month, Guid branchId);
    Task<MonthlyBudgetDto> SetMonthlyBudgetAsync(SetMonthlyBudgetDto dto, string userId);
}

public interface IFinancialPeriodService
{
    Task<FinancialPeriodDto> GetOrCreateCurrentPeriodAsync(Guid branchId, int year, int month);
    Task<FinancialPeriodClosingDto> ClosePeriodAsync(ClosePeriodDto dto, string userId, string userName, string? ipAddress, string? deviceInfo);
    Task<bool> ReopenPeriodAsync(ReopenPeriodDto dto, string userId, string userName, string? ipAddress, string? deviceInfo);
    Task<bool> IsPeriodClosedAsync(Guid branchId, DateTime date);
}

public interface IAuditLogService
{
    Task LogAsync(string entityName, string recordId, Core.Enums.FinancialAuditAction action, object? oldValues, object? newValues, string userId, string userName, string? ipAddress, string? deviceInfo, string? remarks = null);
    Task<IEnumerable<FinancialAuditLogDto>> GetLogsForRecordAsync(string entityName, string recordId);
}
