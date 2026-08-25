using Microsoft.EntityFrameworkCore;
using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Core.Entities.Financial;
using SeddikClinic.Core.Enums;
using SeddikClinic.Core.Interfaces;
using SeddikClinic.Infrastructure.Data;

namespace SeddikClinic.Infrastructure.Services;

public class ExpenseService : IExpenseService
{
    private readonly SeddikClinicDbContext _dbContext;
    private readonly IFinancialPeriodService _periodService;
    private readonly IAuditLogService _auditLogService;
    private readonly IFileStorageService _fileStorageService;

    public ExpenseService(
        SeddikClinicDbContext dbContext,
        IFinancialPeriodService periodService,
        IAuditLogService auditLogService,
        IFileStorageService fileStorageService)
    {
        _dbContext = dbContext;
        _periodService = periodService;
        _auditLogService = auditLogService;
        _fileStorageService = fileStorageService;
    }

    public async Task<IEnumerable<ExpenseDto>> GetExpensesAsync(ExpenseFilterDto filter)
    {
        var query = BuildFilterQuery(filter);

        var expenses = await query
            .Include(e => e.Category)
            .Include(e => e.Attachments)
            .OrderByDescending(e => e.PaymentDate)
            .Skip((filter.PageIndex - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return expenses.Select(MapToExpenseDto);
    }

    public async Task<int> GetExpensesCountAsync(ExpenseFilterDto filter)
    {
        var query = BuildFilterQuery(filter);
        return await query.CountAsync();
    }

    public async Task<ExpenseDto?> GetExpenseByIdAsync(Guid id)
    {
        var expense = await _dbContext.Expenses
            .Include(e => e.Category)
            .Include(e => e.Attachments)
            .FirstOrDefaultAsync(e => e.Id == id);

        return expense != null ? MapToExpenseDto(expense) : null;
    }

    public async Task<ExpenseDto> CreateExpenseAsync(
        CreateExpenseDto dto, 
        string userId, 
        string userName, 
        string? ipAddress, 
        string? deviceInfo)
    {
        // التحقق من أن الفترة المالية ليست مقفلة
        if (await _periodService.IsPeriodClosedAsync(dto.BranchId, dto.PaymentDate))
        {
            throw new InvalidOperationException("لا يمكن إضافة مصروف في فترة مالية مقفلة.");
        }

        // توليد رقم المصروف التسلسلي
        var datePrefix = dto.PaymentDate.ToString("yyyyMM");
        var countThisMonth = await _dbContext.Expenses
            .IgnoreQueryFilters()
            .CountAsync(e => e.PaymentDate.Year == dto.PaymentDate.Year && e.PaymentDate.Month == dto.PaymentDate.Month);

        var expenseNumber = $"EXP-{datePrefix}-{(countThisMonth + 1):D4}";

        var expense = new Expense
        {
            ExpenseNumber = expenseNumber,
            Title = dto.Title,
            CategoryId = dto.CategoryId,
            Amount = dto.Amount,
            PaymentDate = dto.PaymentDate,
            PaymentMethod = dto.PaymentMethod,
            RecurrenceType = dto.RecurrenceType,
            BeneficiaryName = dto.BeneficiaryName,
            ReceiptNumber = dto.ReceiptNumber,
            Notes = dto.Notes,
            Status = dto.Status,
            BranchId = dto.BranchId,
            DoctorId = dto.DoctorId,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId,
            CreatedByUserName = userName
        };

        _dbContext.Expenses.Add(expense);
        await _dbContext.SaveChangesAsync();

        // تدقيق العمليات (Audit Log)
        await _auditLogService.LogAsync(
            entityName: nameof(Expense),
            recordId: expense.Id.ToString(),
            action: FinancialAuditAction.Create,
            oldValues: null,
            newValues: new
            {
                expense.ExpenseNumber,
                expense.Title,
                expense.Amount,
                expense.CategoryId,
                expense.PaymentDate,
                expense.PaymentMethod,
                expense.Status
            },
            userId: userId,
            userName: userName,
            ipAddress: ipAddress,
            deviceInfo: deviceInfo
        );

        return (await GetExpenseByIdAsync(expense.Id))!;
    }

    public async Task<ExpenseDto> UpdateExpenseAsync(
        Guid id, 
        UpdateExpenseDto dto, 
        string userId, 
        string userName, 
        string? ipAddress, 
        string? deviceInfo)
    {
        var expense = await _dbContext.Expenses.FirstOrDefaultAsync(e => e.Id == id);
        if (expense == null)
            throw new KeyNotFoundException("المصروف غير موجود.");

        // فحص إقفال الفترة المالية التاريخية والجديدة
        if (await _periodService.IsPeriodClosedAsync(expense.BranchId, expense.PaymentDate) ||
            await _periodService.IsPeriodClosedAsync(expense.BranchId, dto.PaymentDate))
        {
            throw new InvalidOperationException("لا يمكن تعديل مصروف يقع في فترة مالية مقفلة.");
        }

        var oldValues = new
        {
            expense.Title,
            expense.Amount,
            expense.CategoryId,
            expense.PaymentDate,
            expense.PaymentMethod,
            expense.Status,
            expense.Notes
        };

        expense.Title = dto.Title;
        expense.CategoryId = dto.CategoryId;
        expense.Amount = dto.Amount;
        expense.PaymentDate = dto.PaymentDate;
        expense.PaymentMethod = dto.PaymentMethod;
        expense.RecurrenceType = dto.RecurrenceType;
        expense.BeneficiaryName = dto.BeneficiaryName;
        expense.ReceiptNumber = dto.ReceiptNumber;
        expense.Notes = dto.Notes;
        expense.Status = dto.Status;
        expense.DoctorId = dto.DoctorId;
        expense.UpdatedAt = DateTime.UtcNow;
        expense.UpdatedByUserId = userId;

        await _dbContext.SaveChangesAsync();

        await _auditLogService.LogAsync(
            entityName: nameof(Expense),
            recordId: expense.Id.ToString(),
            action: FinancialAuditAction.Update,
            oldValues: oldValues,
            newValues: new
            {
                expense.Title,
                expense.Amount,
                expense.CategoryId,
                expense.PaymentDate,
                expense.PaymentMethod,
                expense.Status,
                expense.Notes
            },
            userId: userId,
            userName: userName,
            ipAddress: ipAddress,
            deviceInfo: deviceInfo
        );

        return (await GetExpenseByIdAsync(expense.Id))!;
    }

    public async Task<bool> CancelExpenseAsync(
        Guid id, 
        string reason, 
        string userId, 
        string userName, 
        string? ipAddress, 
        string? deviceInfo)
    {
        var expense = await _dbContext.Expenses.FirstOrDefaultAsync(e => e.Id == id);
        if (expense == null)
            throw new KeyNotFoundException("المصروف غير موجود.");

        if (await _periodService.IsPeriodClosedAsync(expense.BranchId, expense.PaymentDate))
        {
            throw new InvalidOperationException("لا يمكن إلغاء مصروف في فترة مالية مقفلة.");
        }

        var oldStatus = expense.Status;
        expense.Status = ExpenseStatus.Cancelled;
        expense.CancellationReason = reason;
        expense.UpdatedAt = DateTime.UtcNow;
        expense.UpdatedByUserId = userId;

        await _dbContext.SaveChangesAsync();

        await _auditLogService.LogAsync(
            entityName: nameof(Expense),
            recordId: expense.Id.ToString(),
            action: FinancialAuditAction.Cancel,
            oldValues: new { Status = oldStatus },
            newValues: new { Status = ExpenseStatus.Cancelled, CancellationReason = reason },
            userId: userId,
            userName: userName,
            ipAddress: ipAddress,
            deviceInfo: deviceInfo,
            remarks: reason
        );

        return true;
    }

    public async Task<ExpenseAttachmentDto> AddAttachmentAsync(
        Guid expenseId, 
        Stream stream, 
        string fileName, 
        string contentType, 
        string userId)
    {
        var expense = await _dbContext.Expenses.FirstOrDefaultAsync(e => e.Id == expenseId);
        if (expense == null)
            throw new KeyNotFoundException("المصروف غير موجود.");

        var uploadResult = await _fileStorageService.UploadFileAsync(stream, fileName, contentType, "expenses");
        if (!uploadResult.Success)
            throw new InvalidOperationException($"فشل رفع المرفق: {uploadResult.ErrorMessage}");

        var attachment = new ExpenseAttachment
        {
            ExpenseId = expenseId,
            FileName = uploadResult.FileName,
            OriginalFileName = uploadResult.OriginalFileName,
            FileUrl = uploadResult.FileUrl,
            ThumbnailUrl = uploadResult.ThumbnailUrl,
            ContentType = uploadResult.ContentType,
            FileSizeBytes = uploadResult.FileSizeBytes,
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = userId
        };

        _dbContext.ExpenseAttachments.Add(attachment);
        await _dbContext.SaveChangesAsync();

        return new ExpenseAttachmentDto
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            OriginalFileName = attachment.OriginalFileName,
            FileUrl = attachment.FileUrl,
            ThumbnailUrl = attachment.ThumbnailUrl,
            ContentType = attachment.ContentType,
            FileSizeBytes = attachment.FileSizeBytes
        };
    }

    public async Task<IEnumerable<ExpenseCategoryDto>> GetCategoriesAsync()
    {
        return await _dbContext.ExpenseCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new ExpenseCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                NameAr = c.NameAr,
                Icon = c.Icon,
                ColorHex = c.ColorHex,
                IsActive = c.IsActive,
                IsDirectCost = c.IsDirectCost
            })
            .ToListAsync();
    }

    // المصروفات الدورية
    public async Task<IEnumerable<RecurringExpenseDto>> GetRecurringExpensesAsync(Guid? branchId)
    {
        var query = _dbContext.RecurringExpenses.Include(r => r.Category).AsQueryable();
        if (branchId.HasValue) query = query.Where(r => r.BranchId == branchId.Value);

        return await query
            .OrderBy(r => r.DayOfMonth)
            .Select(r => new RecurringExpenseDto
            {
                Id = r.Id,
                Title = r.Title,
                CategoryId = r.CategoryId,
                CategoryNameAr = r.Category != null ? r.Category.NameAr : string.Empty,
                Amount = r.Amount,
                DayOfMonth = r.DayOfMonth,
                StartDate = r.StartDate,
                EndDate = r.EndDate,
                LastGeneratedDate = r.LastGeneratedDate,
                AutoCreate = r.AutoCreate,
                AlertBeforeDays = r.AlertBeforeDays,
                IsActive = r.IsActive,
                BranchId = r.BranchId,
                BeneficiaryName = r.BeneficiaryName,
                Notes = r.Notes
            })
            .ToListAsync();
    }

    public async Task<RecurringExpenseDto> CreateRecurringExpenseAsync(CreateRecurringExpenseDto dto, string userId)
    {
        var recurring = new RecurringExpense
        {
            Title = dto.Title,
            CategoryId = dto.CategoryId,
            Amount = dto.Amount,
            DayOfMonth = Math.Clamp(dto.DayOfMonth, 1, 31),
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            AutoCreate = dto.AutoCreate,
            AlertBeforeDays = dto.AlertBeforeDays,
            BranchId = dto.BranchId,
            BeneficiaryName = dto.BeneficiaryName,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        _dbContext.RecurringExpenses.Add(recurring);
        await _dbContext.SaveChangesAsync();

        return (await GetRecurringExpensesAsync(recurring.BranchId)).First(r => r.Id == recurring.Id);
    }

    public async Task<bool> ToggleRecurringExpenseAsync(Guid id, bool isActive)
    {
        var recurring = await _dbContext.RecurringExpenses.FirstOrDefaultAsync(r => r.Id == id);
        if (recurring == null) return false;

        recurring.IsActive = isActive;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<int> ProcessDueRecurringExpensesAsync()
    {
        var today = DateTime.UtcNow.Date;
        var activeRecurring = await _dbContext.RecurringExpenses
            .Where(r => r.IsActive && r.AutoCreate && r.StartDate <= today && (!r.EndDate.HasValue || r.EndDate.Value >= today))
            .ToListAsync();

        int generatedCount = 0;

        foreach (var item in activeRecurring)
        {
            // التحقق من أنه لم يتم إنشاؤه لهذا الشهر الحالي بعد
            var alreadyGeneratedThisMonth = item.LastGeneratedDate.HasValue &&
                item.LastGeneratedDate.Value.Year == today.Year &&
                item.LastGeneratedDate.Value.Month == today.Month;

            if (!alreadyGeneratedThisMonth && today.Day >= item.DayOfMonth)
            {
                var paymentDate = new DateTime(today.Year, today.Month, Math.Min(item.DayOfMonth, DateTime.DaysInMonth(today.Year, today.Month)), 0, 0, 0, DateTimeKind.Utc);
                
                // فحص إقفال الفترة
                if (await _periodService.IsPeriodClosedAsync(item.BranchId, paymentDate))
                    continue;

                var datePrefix = paymentDate.ToString("yyyyMM");
                var countThisMonth = await _dbContext.Expenses
                    .IgnoreQueryFilters()
                    .CountAsync(e => e.PaymentDate.Year == paymentDate.Year && e.PaymentDate.Month == paymentDate.Month);

                var expense = new Expense
                {
                    ExpenseNumber = $"EXP-{datePrefix}-{(countThisMonth + 1):D4}",
                    Title = item.Title,
                    CategoryId = item.CategoryId,
                    Amount = item.Amount,
                    PaymentDate = paymentDate,
                    PaymentMethod = ExpensePaymentMethod.BankTransfer,
                    RecurrenceType = ExpenseRecurrenceType.Monthly,
                    BeneficiaryName = item.BeneficiaryName,
                    Notes = $"تم الإنشاء آلياً من المصروف الدوري: {item.Title}",
                    Status = ExpenseStatus.Accrued, // يُنشأ كمصروف مستحق لحين تأكيد دفعه
                    BranchId = item.BranchId,
                    DoctorId = item.DoctorId,
                    RecurringExpenseId = item.Id,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = "SYSTEM_WORKER",
                    CreatedByUserName = "النظام الآلي"
                };

                _dbContext.Expenses.Add(expense);
                item.LastGeneratedDate = today;
                generatedCount++;
            }
        }

        if (generatedCount > 0)
        {
            await _dbContext.SaveChangesAsync();
        }

        return generatedCount;
    }

    // الموازنات الشهرية
    public async Task<IEnumerable<MonthlyBudgetDto>> GetMonthlyBudgetsAsync(int year, int month, Guid branchId)
    {
        var categories = await _dbContext.ExpenseCategories.Where(c => c.IsActive).ToListAsync();
        var budgets = await _dbContext.MonthlyBudgets
            .Where(b => b.Year == year && b.Month == month && b.BranchId == branchId)
            .ToListAsync();

        var spentPerCategory = await _dbContext.Expenses
            .Where(e => e.BranchId == branchId && e.PaymentDate.Year == year && e.PaymentDate.Month == month && e.Status == ExpenseStatus.Paid)
            .GroupBy(e => e.CategoryId)
            .Select(g => new { CategoryId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Total);

        return categories.Select(c =>
        {
            var budget = budgets.FirstOrDefault(b => b.CategoryId == c.Id);
            var spent = spentPerCategory.GetValueOrDefault(c.Id, 0m);
            var budgetAmount = budget?.BudgetAmount ?? 0m;
            var alertThreshold = budget?.AlertThresholdPercent ?? 85;

            return new MonthlyBudgetDto
            {
                Id = budget?.Id ?? Guid.Empty,
                CategoryId = c.Id,
                CategoryNameAr = c.NameAr,
                BranchId = branchId,
                Year = year,
                Month = month,
                BudgetAmount = budgetAmount,
                ActualSpentAmount = spent,
                AlertThresholdPercent = alertThreshold
            };
        });
    }

    public async Task<MonthlyBudgetDto> SetMonthlyBudgetAsync(SetMonthlyBudgetDto dto, string userId)
    {
        var existing = await _dbContext.MonthlyBudgets
            .FirstOrDefaultAsync(b => b.CategoryId == dto.CategoryId && b.BranchId == dto.BranchId && b.Year == dto.Year && b.Month == dto.Month);

        if (existing != null)
        {
            existing.BudgetAmount = dto.BudgetAmount;
            existing.AlertThresholdPercent = dto.AlertThresholdPercent;
        }
        else
        {
            existing = new MonthlyBudget
            {
                CategoryId = dto.CategoryId,
                BranchId = dto.BranchId,
                Year = dto.Year,
                Month = dto.Month,
                BudgetAmount = dto.BudgetAmount,
                AlertThresholdPercent = dto.AlertThresholdPercent,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId
            };
            _dbContext.MonthlyBudgets.Add(existing);
        }

        await _dbContext.SaveChangesAsync();

        var list = await GetMonthlyBudgetsAsync(dto.Year, dto.Month, dto.BranchId);
        return list.First(b => b.CategoryId == dto.CategoryId);
    }

    private IQueryable<Expense> BuildFilterQuery(ExpenseFilterDto filter)
    {
        var query = _dbContext.Expenses.AsQueryable();

        if (filter.BranchId.HasValue) query = query.Where(e => e.BranchId == filter.BranchId.Value);
        if (filter.DoctorId.HasValue) query = query.Where(e => e.DoctorId == filter.DoctorId.Value);
        if (filter.CategoryId.HasValue) query = query.Where(e => e.CategoryId == filter.CategoryId.Value);
        if (filter.Status.HasValue) query = query.Where(e => e.Status == filter.Status.Value);
        if (filter.PaymentMethod.HasValue) query = query.Where(e => e.PaymentMethod == filter.PaymentMethod.Value);
        if (filter.FromDate.HasValue) query = query.Where(e => e.PaymentDate >= filter.FromDate.Value);
        if (filter.ToDate.HasValue) query = query.Where(e => e.PaymentDate <= filter.ToDate.Value);
        if (filter.MinAmount.HasValue) query = query.Where(e => e.Amount >= filter.MinAmount.Value);
        if (filter.MaxAmount.HasValue) query = query.Where(e => e.Amount <= filter.MaxAmount.Value);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.Title.ToLower().Contains(term) ||
                                     e.ExpenseNumber.ToLower().Contains(term) ||
                                     (e.BeneficiaryName != null && e.BeneficiaryName.ToLower().Contains(term)) ||
                                     (e.ReceiptNumber != null && e.ReceiptNumber.ToLower().Contains(term)));
        }

        return query;
    }

    private static ExpenseDto MapToExpenseDto(Expense e)
    {
        return new ExpenseDto
        {
            Id = e.Id,
            ExpenseNumber = e.ExpenseNumber,
            Title = e.Title,
            CategoryId = e.CategoryId,
            CategoryNameAr = e.Category != null ? e.Category.NameAr : string.Empty,
            CategoryColorHex = e.Category?.ColorHex,
            Amount = e.Amount,
            PaymentDate = e.PaymentDate,
            PaymentMethod = e.PaymentMethod,
            PaymentMethodNameAr = e.PaymentMethod switch
            {
                ExpensePaymentMethod.Cash => "نقداً",
                ExpensePaymentMethod.DebitCreditCard => "بطاقة مدى / ائتمان",
                ExpensePaymentMethod.BankTransfer => "تحويل بنكي",
                ExpensePaymentMethod.Cheque => "شيك",
                _ => e.PaymentMethod.ToString()
            },
            RecurrenceType = e.RecurrenceType,
            BeneficiaryName = e.BeneficiaryName,
            ReceiptNumber = e.ReceiptNumber,
            Notes = e.Notes,
            Status = e.Status,
            StatusNameAr = e.Status switch
            {
                ExpenseStatus.Paid => "مدفوع",
                ExpenseStatus.Accrued => "مستحق غير مدفوع",
                ExpenseStatus.Cancelled => "ملغي",
                ExpenseStatus.Refunded => "مسترد",
                _ => e.Status.ToString()
            },
            BranchId = e.BranchId,
            DoctorId = e.DoctorId,
            CreatedAt = e.CreatedAt,
            CreatedByUserName = e.CreatedByUserName,
            Attachments = e.Attachments.Select(a => new ExpenseAttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                OriginalFileName = a.OriginalFileName,
                FileUrl = a.FileUrl,
                ThumbnailUrl = a.ThumbnailUrl,
                ContentType = a.ContentType,
                FileSizeBytes = a.FileSizeBytes
            }).ToList()
        };
    }
}
