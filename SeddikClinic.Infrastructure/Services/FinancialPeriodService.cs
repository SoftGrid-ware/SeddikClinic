using Microsoft.EntityFrameworkCore;
using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Core.Entities.Financial;
using SeddikClinic.Core.Enums;
using SeddikClinic.Core.Interfaces;
using SeddikClinic.Infrastructure.Data;

namespace SeddikClinic.Infrastructure.Services;

public class FinancialPeriodService : IFinancialPeriodService
{
    private readonly SeddikClinicDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;

    public FinancialPeriodService(SeddikClinicDbContext dbContext, IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
    }

    public async Task<FinancialPeriodDto> GetOrCreateCurrentPeriodAsync(Guid branchId, int year, int month)
    {
        var period = await _dbContext.FinancialPeriods
            .Include(p => p.ClosingDetails)
            .FirstOrDefaultAsync(p => p.BranchId == branchId && p.Year == year && p.Month == month);

        if (period == null)
        {
            var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = startDate.AddMonths(1).AddTicks(-1);

            period = new FinancialPeriod
            {
                BranchId = branchId,
                Year = year,
                Month = month,
                StartDate = startDate,
                EndDate = endDate,
                Status = FinancialPeriodStatus.Open
            };

            _dbContext.FinancialPeriods.Add(period);
            await _dbContext.SaveChangesAsync();
        }

        return MapToDto(period);
    }

    public async Task<bool> IsPeriodClosedAsync(Guid branchId, DateTime date)
    {
        var year = date.Year;
        var month = date.Month;

        var period = await _dbContext.FinancialPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.BranchId == branchId && p.Year == year && p.Month == month);

        return period != null && period.Status == FinancialPeriodStatus.Closed;
    }

    public async Task<FinancialPeriodClosingDto> ClosePeriodAsync(
        ClosePeriodDto dto, 
        string userId, 
        string userName, 
        string? ipAddress, 
        string? deviceInfo)
    {
        var period = await _dbContext.FinancialPeriods
            .Include(p => p.ClosingDetails)
            .FirstOrDefaultAsync(p => p.Id == dto.PeriodId);

        if (period == null)
            throw new KeyNotFoundException("الفترة المالية غير موجودة.");

        if (period.Status == FinancialPeriodStatus.Closed)
            throw new InvalidOperationException("هذه الفترة المالية مقفلة مسبقاً.");

        // احتساب الأرقام المالية الدقيقة للفترة وقت الإقفال
        var startDate = period.StartDate;
        var endDate = period.EndDate;

        var totalRevenueCollected = await _dbContext.PatientPayments
            .Where(p => p.BranchId == period.BranchId && p.PaymentDate >= startDate && p.PaymentDate <= endDate)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var totalRefunds = await _dbContext.PatientRefunds
            .Where(r => r.BranchId == period.BranchId && r.RefundDate >= startDate && r.RefundDate <= endDate)
            .SumAsync(r => (decimal?)r.Amount) ?? 0m;

        var totalExpensesPaid = await _dbContext.Expenses
            .Where(e => e.BranchId == period.BranchId && e.PaymentDate >= startDate && e.PaymentDate <= endDate && e.Status == ExpenseStatus.Paid)
            .SumAsync(e => (decimal?)e.Amount) ?? 0m;

        var totalAccruedExpenses = await _dbContext.Expenses
            .Where(e => e.BranchId == period.BranchId && e.PaymentDate >= startDate && e.PaymentDate <= endDate && e.Status == ExpenseStatus.Accrued)
            .SumAsync(e => (decimal?)e.Amount) ?? 0m;

        var invoices = await _dbContext.PatientInvoices
            .Where(i => i.BranchId == period.BranchId && i.InvoiceDate >= startDate && i.InvoiceDate <= endDate)
            .ToListAsync();

        var totalInvoicesAmount = invoices.Sum(i => i.TotalAmount);
        var totalInvoicePaid = invoices.Sum(i => i.PaidAmount);
        var totalUncollectedReceivables = Math.Max(0, totalInvoicesAmount - totalInvoicePaid);

        var netCashFlow = totalRevenueCollected - totalRefunds - totalExpensesPaid;

        period.Status = FinancialPeriodStatus.Closed;

        FinancialPeriodClosing closing;
        if (period.ClosingDetails != null)
        {
            closing = period.ClosingDetails;
            closing.ClosedByUserId = userId;
            closing.ClosedByUserName = userName;
            closing.ClosedAt = DateTime.UtcNow;
            closing.TotalRevenueCollected = totalRevenueCollected;
            closing.TotalExpensesPaid = totalExpensesPaid;
            closing.NetCashFlow = netCashFlow;
            closing.TotalUncollectedReceivables = totalUncollectedReceivables;
            closing.TotalAccruedExpenses = totalAccruedExpenses;
            closing.Notes = dto.Notes;
            closing.IsReopened = false;
        }
        else
        {
            closing = new FinancialPeriodClosing
            {
                PeriodId = period.Id,
                ClosedByUserId = userId,
                ClosedByUserName = userName,
                ClosedAt = DateTime.UtcNow,
                TotalRevenueCollected = totalRevenueCollected,
                TotalExpensesPaid = totalExpensesPaid,
                NetCashFlow = netCashFlow,
                TotalUncollectedReceivables = totalUncollectedReceivables,
                TotalAccruedExpenses = totalAccruedExpenses,
                Notes = dto.Notes
            };
            _dbContext.FinancialPeriodClosings.Add(closing);
            period.ClosingDetails = closing;
        }

        await _dbContext.SaveChangesAsync();

        // تسجيل في الـ Audit Log
        await _auditLogService.LogAsync(
            entityName: nameof(FinancialPeriod),
            recordId: period.Id.ToString(),
            action: FinancialAuditAction.PeriodClose,
            oldValues: new { Status = "Open" },
            newValues: new { Status = "Closed", NetCashFlow = netCashFlow, ClosedAt = closing.ClosedAt },
            userId: userId,
            userName: userName,
            ipAddress: ipAddress,
            deviceInfo: deviceInfo,
            remarks: dto.Notes
        );

        return new FinancialPeriodClosingDto
        {
            Id = closing.Id,
            ClosedByUserName = userName,
            ClosedAt = closing.ClosedAt,
            TotalRevenueCollected = closing.TotalRevenueCollected,
            TotalExpensesPaid = closing.TotalExpensesPaid,
            NetCashFlow = closing.NetCashFlow,
            TotalUncollectedReceivables = closing.TotalUncollectedReceivables,
            TotalAccruedExpenses = closing.TotalAccruedExpenses,
            Notes = closing.Notes
        };
    }

    public async Task<bool> ReopenPeriodAsync(
        ReopenPeriodDto dto, 
        string userId, 
        string userName, 
        string? ipAddress, 
        string? deviceInfo)
    {
        var period = await _dbContext.FinancialPeriods
            .Include(p => p.ClosingDetails)
            .FirstOrDefaultAsync(p => p.Id == dto.PeriodId);

        if (period == null)
            throw new KeyNotFoundException("الفترة المالية غير موجودة.");

        if (period.Status == FinancialPeriodStatus.Open)
            throw new InvalidOperationException("الفترة المالية مفتوحة بالفعل.");

        if (string.IsNullOrWhiteSpace(dto.ReopenReason))
            throw new ArgumentException("يجب كتابة سبب إعادة فتح الفترة المالية.");

        period.Status = FinancialPeriodStatus.Open;

        if (period.ClosingDetails != null)
        {
            period.ClosingDetails.IsReopened = true;
            period.ClosingDetails.ReopenedByUserId = userId;
            period.ClosingDetails.ReopenedByUserName = userName;
            period.ClosingDetails.ReopenedAt = DateTime.UtcNow;
            period.ClosingDetails.ReopenReason = dto.ReopenReason;
        }

        await _dbContext.SaveChangesAsync();

        await _auditLogService.LogAsync(
            entityName: nameof(FinancialPeriod),
            recordId: period.Id.ToString(),
            action: FinancialAuditAction.PeriodReopen,
            oldValues: new { Status = "Closed" },
            newValues: new { Status = "Open", ReopenReason = dto.ReopenReason },
            userId: userId,
            userName: userName,
            ipAddress: ipAddress,
            deviceInfo: deviceInfo,
            remarks: dto.ReopenReason
        );

        return true;
    }

    private static FinancialPeriodDto MapToDto(FinancialPeriod p)
    {
        return new FinancialPeriodDto
        {
            Id = p.Id,
            BranchId = p.BranchId,
            Year = p.Year,
            Month = p.Month,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            Status = p.Status,
            ClosingDetails = p.ClosingDetails != null ? new FinancialPeriodClosingDto
            {
                Id = p.ClosingDetails.Id,
                ClosedByUserName = p.ClosingDetails.ClosedByUserName,
                ClosedAt = p.ClosingDetails.ClosedAt,
                TotalRevenueCollected = p.ClosingDetails.TotalRevenueCollected,
                TotalExpensesPaid = p.ClosingDetails.TotalExpensesPaid,
                NetCashFlow = p.ClosingDetails.NetCashFlow,
                TotalUncollectedReceivables = p.ClosingDetails.TotalUncollectedReceivables,
                TotalAccruedExpenses = p.ClosingDetails.TotalAccruedExpenses,
                Notes = p.ClosingDetails.Notes,
                IsReopened = p.ClosingDetails.IsReopened,
                ReopenedByUserName = p.ClosingDetails.ReopenedByUserName,
                ReopenedAt = p.ClosingDetails.ReopenedAt,
                ReopenReason = p.ClosingDetails.ReopenReason
            } : null
        };
    }
}
