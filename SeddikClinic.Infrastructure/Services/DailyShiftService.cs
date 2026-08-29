using Microsoft.EntityFrameworkCore;
using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Core.Entities.Appointments;
using SeddikClinic.Core.Entities.Billing;
using SeddikClinic.Core.Entities.Financial;
using SeddikClinic.Core.Enums;
using SeddikClinic.Core.Interfaces;
using SeddikClinic.Infrastructure.Data;

namespace SeddikClinic.Infrastructure.Services;

public class DailyShiftService : IDailyShiftService
{
    private readonly SeddikClinicDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;

    public DailyShiftService(SeddikClinicDbContext dbContext, IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
    }

    public async Task<DailyShiftSummaryDto> GetCurrentShiftAsync(Guid? branchId = null)
    {
        var localToday = DateTime.Today;
        var todayStart = DateTime.SpecifyKind(localToday, DateTimeKind.Utc);
        var todayEnd = todayStart.AddDays(1);

        var query = _dbContext.DailyShifts.AsQueryable();
        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            query = query.Where(s => s.BranchId == branchId.Value);
        }

        // البحث أولاً عن وردية مفتوحة حالياً
        var activeShift = await query
            .OrderByDescending(s => s.OpenedAt)
            .FirstOrDefaultAsync(s => s.Status == DailyShiftStatus.Open);

        if (activeShift != null)
        {
            return await CalculateLiveShiftMetricsAsync(activeShift);
        }

        // إذا لم توجد وردية مفتوحة، فحص آخر وردية أغلقت اليوم
        var lastTodayShift = await query
            .Where(s => s.ShiftDate >= todayStart && s.ShiftDate < todayEnd)
            .OrderByDescending(s => s.OpenedAt)
            .FirstOrDefaultAsync();

        if (lastTodayShift != null)
        {
            return MapToDto(lastTodayShift);
        }

        // إنشاء تمثيل افتراضي لوردية اليوم (غير مفتوحة بعد)
        return new DailyShiftSummaryDto
        {
            ShiftDate = DateTime.Today,
            ShiftNumber = $"SH-{DateTime.Today:yyMMdd}-01",
            ShiftType = DailyShiftType.Morning,
            Status = DailyShiftStatus.Open,
            OpeningCashBalance = 500m,
            ExpectedCashInDrawer = 500m,
            DifferenceStatus = ShiftDifferenceStatus.Balanced
        };
    }

    public async Task<DailyShiftSummaryDto> OpenShiftAsync(OpenShiftRequestDto dto, string userId, string userName)
    {
        var localToday = DateTime.Today;
        var todayStart = DateTime.SpecifyKind(localToday, DateTimeKind.Utc);
        var todayEnd = todayStart.AddDays(1);

        // التحقق من عدم وجود وردية مفتوحة حالياً لنفس الفرع
        var existingOpen = await _dbContext.DailyShifts
            .FirstOrDefaultAsync(s => s.Status == DailyShiftStatus.Open && (dto.BranchId == Guid.Empty || s.BranchId == dto.BranchId));

        if (existingOpen != null)
        {
            throw new InvalidOperationException($"يوجد وردية مفتوحة بالفعل برقم {existingOpen.ShiftNumber} بواسطة {existingOpen.OpenedByUserName}. يجب إغلاقها أولاً قبل فتح وردية جديدة.");
        }

        // توليد رقم وردية تسلسلي
        var countToday = await _dbContext.DailyShifts
            .CountAsync(s => s.ShiftDate >= todayStart && s.ShiftDate < todayEnd);

        var shiftNumber = $"SH-{DateTime.Today:yyMMdd}-{(countToday + 1):D2}";

        var newShift = new DailyShift
        {
            BranchId = dto.BranchId,
            ShiftNumber = shiftNumber,
            ShiftDate = todayStart,
            ShiftType = dto.ShiftType,
            Status = DailyShiftStatus.Open,
            OpenedAt = DateTime.UtcNow,
            OpenedByUserId = userId,
            OpenedByUserName = userName,
            OpeningCashBalance = dto.OpeningCashBalance,
            HandoverNotes = dto.Notes
        };

        _dbContext.DailyShifts.Add(newShift);
        await _dbContext.SaveChangesAsync();

        await _auditLogService.LogAsync(
            entityName: nameof(DailyShift),
            recordId: newShift.Id.ToString(),
            action: FinancialAuditAction.ShiftOpen,
            oldValues: null,
            newValues: new { newShift.ShiftNumber, newShift.OpeningCashBalance, userName },
            userId: userId,
            userName: userName,
            ipAddress: null,
            deviceInfo: null,
            remarks: $"فتح وردية جديدة رقم {shiftNumber} برصيد افتتاح {dto.OpeningCashBalance:N2} ج.م"
        );

        return await CalculateLiveShiftMetricsAsync(newShift);
    }

    public async Task<DailyShiftSummaryDto> CloseShiftAsync(CloseShiftRequestDto dto, string userId, string userName)
    {
        var shift = await _dbContext.DailyShifts.FirstOrDefaultAsync(s => s.Id == dto.ShiftId);
        if (shift == null)
        {
            throw new KeyNotFoundException("الوردية غير موجودة.");
        }

        if (shift.Status == DailyShiftStatus.Closed)
        {
            throw new InvalidOperationException("هذه الوردية مقفلة بالفعل.");
        }

        // إعادة حساب الأرقام المحاسبية المحدثة بدقة عند لحظة الإغلاق
        var summary = await CalculateLiveShiftMetricsAsync(shift);

        shift.ClosedAt = DateTime.UtcNow;
        shift.ClosedByUserId = userId;
        shift.ClosedByUserName = userName;
        shift.Status = DailyShiftStatus.Closed;

        shift.TotalCashRevenue = summary.TotalCashRevenue;
        shift.TotalCardRevenue = summary.TotalCardRevenue;
        shift.TotalTransferRevenue = summary.TotalTransferRevenue;
        shift.TotalInstallmentsCollected = summary.TotalInstallmentsCollected;
        shift.TotalCashExpenses = summary.TotalCashExpenses;
        shift.TotalRefunds = summary.TotalRefunds;
        shift.ExpectedCashInDrawer = summary.ExpectedCashInDrawer;

        shift.ActualCashInDrawer = dto.ActualCashInDrawer;
        shift.DifferenceAmount = dto.ActualCashInDrawer - summary.ExpectedCashInDrawer;

        if (shift.DifferenceAmount == 0)
        {
            shift.DifferenceStatus = ShiftDifferenceStatus.Balanced;
        }
        else if (shift.DifferenceAmount > 0)
        {
            shift.DifferenceStatus = ShiftDifferenceStatus.Surplus;
        }
        else
        {
            shift.DifferenceStatus = ShiftDifferenceStatus.Shortage;
        }

        shift.DifferenceReason = dto.DifferenceReason;
        shift.HandoverNotes = dto.HandoverNotes;
        shift.HandoverToUserName = dto.HandoverToUserName;

        shift.AppointmentsCount = summary.AppointmentsCount;
        shift.CompletedAppointmentsCount = summary.CompletedAppointmentsCount;
        shift.InvoicesCount = summary.InvoicesCount;

        await _dbContext.SaveChangesAsync();

        await _auditLogService.LogAsync(
            entityName: nameof(DailyShift),
            recordId: shift.Id.ToString(),
            action: FinancialAuditAction.ShiftClose,
            oldValues: new { shift.ExpectedCashInDrawer },
            newValues: new { shift.ActualCashInDrawer, shift.DifferenceAmount, shift.DifferenceStatus, userName },
            userId: userId,
            userName: userName,
            ipAddress: null,
            deviceInfo: null,
            remarks: $"إغلاق وردية رقم {shift.ShiftNumber} - الفعلي: {shift.ActualCashInDrawer:N2} ج.م - الفارق: {shift.DifferenceAmount:N2} ج.م ({shift.DifferenceStatus})"
        );

        return MapToDto(shift);
    }

    public async Task<List<DailyShiftSummaryDto>> GetShiftHistoryAsync(DateTime? fromDate = null, DateTime? toDate = null, Guid? branchId = null)
    {
        var query = _dbContext.DailyShifts.AsQueryable();

        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            query = query.Where(s => s.BranchId == branchId.Value);
        }

        if (fromDate.HasValue)
        {
            var start = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(s => s.ShiftDate >= start);
        }

        if (toDate.HasValue)
        {
            var end = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(s => s.ShiftDate < end);
        }

        var shifts = await query
            .OrderByDescending(s => s.OpenedAt)
            .Take(100)
            .ToListAsync();

        return shifts.Select(MapToDto).ToList();
    }

    public async Task<DailyShiftSummaryDto> ReopenShiftAsync(Guid shiftId, string userId, string userName, string reason)
    {
        var shift = await _dbContext.DailyShifts.FirstOrDefaultAsync(s => s.Id == shiftId);
        if (shift == null) throw new KeyNotFoundException("الوردية غير موجودة.");

        shift.Status = DailyShiftStatus.Open;
        shift.ClosedAt = null;
        shift.ClosedByUserId = null;
        shift.ClosedByUserName = null;
        shift.HandoverNotes = $"[إعادة فتح بواسطة {userName} بتاريخ {DateTime.Now:yyyy/MM/dd HH:mm}: {reason}] " + shift.HandoverNotes;

        await _dbContext.SaveChangesAsync();

        await _auditLogService.LogAsync(
            entityName: nameof(DailyShift),
            recordId: shift.Id.ToString(),
            action: FinancialAuditAction.PeriodReopen,
            oldValues: null,
            newValues: new { shift.ShiftNumber, reason, userName },
            userId: userId,
            userName: userName,
            ipAddress: null,
            deviceInfo: null,
            remarks: $"إعادة فتح الوردية رقم {shift.ShiftNumber}: {reason}"
        );

        return await CalculateLiveShiftMetricsAsync(shift);
    }

    // =========================================================
    // حسابات المؤشرات المالية الحية للوردية
    // =========================================================

    private async Task<DailyShiftSummaryDto> CalculateLiveShiftMetricsAsync(DailyShift shift)
    {
        var startTime = shift.OpenedAt;
        var endTime = shift.ClosedAt ?? DateTime.UtcNow;

        var shiftDateStart = DateTime.SpecifyKind(shift.ShiftDate.Date, DateTimeKind.Utc);
        var shiftDateEnd = shiftDateStart.AddDays(1);

        // 1. حجوزات وكشوفات الوردية
        var apts = await _dbContext.Appointments
            .Where(a => !a.IsDeleted && a.AppointmentDate >= shiftDateStart && a.AppointmentDate < shiftDateEnd)
            .ToListAsync();

        // 2. مدفوعات وأقساط المرضى
        var payments = await _dbContext.PatientPayments
            .Where(p => p.PaymentDate >= startTime || (p.PaymentDate >= shiftDateStart && p.PaymentDate < shiftDateEnd))
            .ToListAsync();

        // 3. المصروفات النقدية المسددة من الخزينة/الدرج
        var expenses = await _dbContext.Expenses
            .Where(e => e.Status == ExpenseStatus.Paid && 
                        e.PaymentMethod == ExpensePaymentMethod.Cash &&
                        ((e.PaymentDate >= startTime) || (e.PaymentDate >= shiftDateStart && e.PaymentDate < shiftDateEnd)))
            .ToListAsync();

        // 4. المبالغ المستردة للمرضى
        var refunds = await _dbContext.PatientRefunds
            .Where(r => (r.RefundDate >= startTime) || (r.RefundDate >= shiftDateStart && r.RefundDate < shiftDateEnd))
            .ToListAsync();

        // 5. الفواتير
        var invoices = await _dbContext.PatientInvoices
            .Where(i => i.InvoiceDate >= shiftDateStart && i.InvoiceDate < shiftDateEnd)
            .ToListAsync();

        // احتساب المقبوضات
        decimal totalCash = 0m;
        decimal totalCard = 0m;
        decimal totalTransfer = 0m;
        decimal totalInstallments = 0m;

        // من الحجوزات
        foreach (var apt in apts.Where(a => a.Status != AppointmentStatus.Cancelled))
        {
            var netFees = Math.Max(0, apt.TotalFees - apt.DiscountAmount);
            decimal collected = apt.DepositAmount > 0 ? Math.Min(apt.DepositAmount, netFees) : 0m;

            if (collected > 0)
            {
                totalCash += collected; // كاش افتراضي للعيادة
            }

            if (apt.DepositAmount > 0)
            {
                totalInstallments += apt.DepositAmount;
            }
        }

        // من سندات القبض والدفعات المباشرة
        foreach (var p in payments)
        {
            // تجنب التكرار إذا كان مسجلاً بالفعل
            if (p.PaymentType == PaymentType.DownPayment || p.PaymentType == PaymentType.PartialPayment)
            {
                totalInstallments += p.Amount;
            }
        }

        var totalCashExpenses = expenses.Sum(e => e.Amount);
        var totalRefunds = refunds.Sum(r => r.Amount);

        var expectedInDrawer = shift.OpeningCashBalance + totalCash - totalCashExpenses - totalRefunds;

        var dto = MapToDto(shift);
        dto.TotalCashRevenue = totalCash;
        dto.TotalCardRevenue = totalCard;
        dto.TotalTransferRevenue = totalTransfer;
        dto.TotalInstallmentsCollected = totalInstallments;
        dto.TotalCashExpenses = totalCashExpenses;
        dto.TotalRefunds = totalRefunds;
        dto.ExpectedCashInDrawer = expectedInDrawer;

        if (shift.Status == DailyShiftStatus.Closed)
        {
            dto.ActualCashInDrawer = shift.ActualCashInDrawer;
            dto.DifferenceAmount = shift.DifferenceAmount;
            dto.DifferenceStatus = shift.DifferenceStatus;
        }
        else
        {
            dto.ActualCashInDrawer = expectedInDrawer; // القيمة المبدئية المقترحة للجرد
            dto.DifferenceAmount = 0m;
            dto.DifferenceStatus = ShiftDifferenceStatus.Balanced;
        }

        dto.AppointmentsCount = apts.Count;
        dto.CompletedAppointmentsCount = apts.Count(a => a.Status == AppointmentStatus.Completed);
        dto.InvoicesCount = invoices.Count;

        return dto;
    }

    private async Task<DailyShiftSummaryDto> BuildVirtualTodayShiftSummaryAsync(Guid branchId, DateTime todayStart, DateTime todayEnd)
    {
        var apts = await _dbContext.Appointments
            .Where(a => !a.IsDeleted && a.AppointmentDate >= todayStart && a.AppointmentDate < todayEnd)
            .ToListAsync();

        var payments = await _dbContext.PatientPayments
            .Where(p => p.PaymentDate >= todayStart && p.PaymentDate < todayEnd)
            .ToListAsync();

        var expenses = await _dbContext.Expenses
            .Where(e => e.Status == ExpenseStatus.Paid && e.PaymentDate >= todayStart && e.PaymentDate < todayEnd)
            .ToListAsync();

        var refunds = await _dbContext.PatientRefunds
            .Where(r => r.RefundDate >= todayStart && r.RefundDate < todayEnd)
            .ToListAsync();

        decimal totalCash = 0m;
        decimal totalInstallments = 0m;

        foreach (var apt in apts.Where(a => a.Status != AppointmentStatus.Cancelled))
        {
            if (apt.DepositAmount > 0)
            {
                totalCash += Math.Min(apt.DepositAmount, apt.TotalFees);
                totalInstallments += apt.DepositAmount;
            }
            else if (apt.IsDepositPaid || apt.Status == AppointmentStatus.Completed)
            {
                totalCash += apt.TotalFees;
            }
        }

        totalCash += payments.Sum(p => p.Amount);
        var totalExpenses = expenses.Sum(e => e.Amount);
        var totalRefunds = refunds.Sum(r => r.Amount);
        var expectedInDrawer = totalCash - totalExpenses - totalRefunds;

        return new DailyShiftSummaryDto
        {
            Id = Guid.Empty,
            ShiftNumber = $"SHIFT-{DateTime.Now:yyyyMMdd}-01",
            BranchId = branchId,
            ShiftDate = todayStart,
            ShiftType = DailyShiftType.FullDay,
            Status = DailyShiftStatus.Open,
            OpenedAt = DateTime.UtcNow,
            OpenedByUserName = "المستخدم الحالي",
            OpeningCashBalance = 0m,
            TotalCashRevenue = totalCash,
            TotalCardRevenue = 0m,
            TotalTransferRevenue = 0m,
            TotalInstallmentsCollected = totalInstallments,
            TotalCashExpenses = totalExpenses,
            TotalRefunds = totalRefunds,
            ExpectedCashInDrawer = expectedInDrawer,
            ActualCashInDrawer = expectedInDrawer,
            DifferenceAmount = 0m,
            DifferenceStatus = ShiftDifferenceStatus.Balanced,
            AppointmentsCount = apts.Count,
            CompletedAppointmentsCount = apts.Count(a => a.Status == AppointmentStatus.Completed),
            InvoicesCount = 0
        };
    }

    private static DailyShiftSummaryDto MapToDto(DailyShift shift)
    {
        return new DailyShiftSummaryDto
        {
            Id = shift.Id,
            ShiftNumber = shift.ShiftNumber,
            BranchId = shift.BranchId,
            ShiftDate = shift.ShiftDate,
            ShiftType = shift.ShiftType,
            Status = shift.Status,
            OpenedAt = shift.OpenedAt,
            OpenedByUserId = shift.OpenedByUserId,
            OpenedByUserName = shift.OpenedByUserName,
            OpeningCashBalance = shift.OpeningCashBalance,
            ClosedAt = shift.ClosedAt,
            ClosedByUserId = shift.ClosedByUserId,
            ClosedByUserName = shift.ClosedByUserName,
            TotalCashRevenue = shift.TotalCashRevenue,
            TotalCardRevenue = shift.TotalCardRevenue,
            TotalTransferRevenue = shift.TotalTransferRevenue,
            TotalInstallmentsCollected = shift.TotalInstallmentsCollected,
            TotalCashExpenses = shift.TotalCashExpenses,
            TotalRefunds = shift.TotalRefunds,
            ExpectedCashInDrawer = shift.ExpectedCashInDrawer,
            ActualCashInDrawer = shift.ActualCashInDrawer,
            DifferenceAmount = shift.DifferenceAmount,
            DifferenceStatus = shift.DifferenceStatus,
            DifferenceReason = shift.DifferenceReason,
            HandoverNotes = shift.HandoverNotes,
            HandoverToUserName = shift.HandoverToUserName,
            AppointmentsCount = shift.AppointmentsCount,
            CompletedAppointmentsCount = shift.CompletedAppointmentsCount,
            InvoicesCount = shift.InvoicesCount
        };
    }
}
