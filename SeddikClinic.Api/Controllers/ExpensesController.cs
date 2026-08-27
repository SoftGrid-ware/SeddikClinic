using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SeddikClinic.Api.Security;
using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Core.Interfaces;

namespace SeddikClinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly IAuditLogService _auditLogService;

    public ExpensesController(IExpenseService expenseService, IAuditLogService auditLogService)
    {
        _expenseService = expenseService;
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// جلب قائمة المصروفات مع الفلترة والبحث والترقيم
    /// </summary>
    [HttpGet]
    [RequireFinancialPermission(FinancialPermissions.ViewDashboard)]
    public async Task<ActionResult<object>> GetExpenses([FromQuery] ExpenseFilterDto filter)
    {
        var items = await _expenseService.GetExpensesAsync(filter);
        var totalCount = await _expenseService.GetExpensesCountAsync(filter);

        return Ok(new
        {
            items,
            totalCount,
            filter.PageIndex,
            filter.PageSize,
            totalPages = (int)Math.Ceiling((double)totalCount / filter.PageSize)
        });
    }

    /// <summary>
    /// جلب تفاصيل مصروف محدد
    /// </summary>
    [HttpGet("{id:guid}")]
    [RequireFinancialPermission(FinancialPermissions.ViewDashboard)]
    public async Task<ActionResult<ExpenseDto>> GetById(Guid id)
    {
        var expense = await _expenseService.GetExpenseByIdAsync(id);
        if (expense == null) return NotFound(new { message = "المصروف غير موجود." });
        return Ok(expense);
    }

    /// <summary>
    /// إضافة مصروف جديد (متاح للطبيب، أو للمساعد الممنوح صلاحية LogExpenseOnly)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ExpenseDto>> Create([FromBody] CreateExpenseDto dto)
    {
        var userId = GetCurrentUserId();
        var userName = GetCurrentUserName();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var device = Request.Headers.UserAgent.ToString();

        try
        {
            var result = await _expenseService.CreateExpenseAsync(dto, userId, userName, ip, device);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// تعديل مصروف مسجل
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequireFinancialPermission(FinancialPermissions.ManageExpenses)]
    public async Task<ActionResult<ExpenseDto>> Update(Guid id, [FromBody] UpdateExpenseDto dto)
    {
        var userId = GetCurrentUserId();
        var userName = GetCurrentUserName();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var device = Request.Headers.UserAgent.ToString();

        try
        {
            var result = await _expenseService.UpdateExpenseAsync(id, dto, userId, userName, ip, device);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// إلغاء مصروف دون حذفه نهائياً
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [RequireFinancialPermission(FinancialPermissions.ManageExpenses)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelExpenseDto dto)
    {
        var userId = GetCurrentUserId();
        var userName = GetCurrentUserName();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var device = Request.Headers.UserAgent.ToString();

        try
        {
            await _expenseService.CancelExpenseAsync(id, dto.CancellationReason, userId, userName, ip, device);
            return Ok(new { message = "تم إلغاء المصروف بنجاح وتسجيل السبب." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// حذف مصروف نهائياً
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await _expenseService.DeleteExpenseAsync(id);
        if (!success) return NotFound(new { message = "المصروف غير موجود." });
        return Ok(new { message = "تم حذف المصروف بنجاح." });
    }

    /// <summary>
    /// رفع إيصال أو مستند مرفق للمصروف مع ضغطه سحابياً
    /// </summary>
    [HttpPost("{id:guid}/attachments")]
    public async Task<ActionResult<ExpenseAttachmentDto>> UploadAttachment(Guid id, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "الملف المرفوع فارغ." });

        var userId = GetCurrentUserId();
        using var stream = file.OpenReadStream();

        try
        {
            var result = await _expenseService.AddAttachmentAsync(id, stream, file.FileName, file.ContentType, userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// جلب تصنيفات المصروفات
    /// </summary>
    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<ExpenseCategoryDto>>> GetCategories()
    {
        var categories = await _expenseService.GetCategoriesAsync();
        return Ok(categories);
    }

    /// <summary>
    /// إضافة تصنيف مصروف جديد
    /// </summary>
    [HttpPost("categories")]
    public async Task<ActionResult<ExpenseCategoryDto>> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NameAr))
            return BadRequest(new { message = "يرجى كتابة اسم التصنيف." });

        var category = await _expenseService.CreateCategoryAsync(request.NameAr.Trim(), request.IsDirectCost);
        return Ok(category);
    }

    /// <summary>
    /// جلب المصروفات الدورية
    /// </summary>
    [HttpGet("recurring")]
    [RequireFinancialPermission(FinancialPermissions.ViewDashboard)]
    public async Task<ActionResult<IEnumerable<RecurringExpenseDto>>> GetRecurring([FromQuery] Guid? branchId)
    {
        var list = await _expenseService.GetRecurringExpensesAsync(branchId);
        return Ok(list);
    }

    /// <summary>
    /// إنشاء مصروف دوري جديد
    /// </summary>
    [HttpPost("recurring")]
    [RequireFinancialPermission(FinancialPermissions.ManageExpenses)]
    public async Task<ActionResult<RecurringExpenseDto>> CreateRecurring([FromBody] CreateRecurringExpenseDto dto)
    {
        var userId = GetCurrentUserId();
        var result = await _expenseService.CreateRecurringExpenseAsync(dto, userId);
        return Ok(result);
    }

    /// <summary>
    /// تفعيل أو إيقاف المصروف الدوري
    /// </summary>
    [HttpPut("recurring/{id:guid}/toggle")]
    [RequireFinancialPermission(FinancialPermissions.ManageExpenses)]
    public async Task<IActionResult> ToggleRecurring(Guid id, [FromBody] bool isActive)
    {
        var success = await _expenseService.ToggleRecurringExpenseAsync(id, isActive);
        if (!success) return NotFound(new { message = "المصروف الدوري غير موجود." });
        return Ok(new { success });
    }

    /// <summary>
    /// جلب الموازنات الشهرية ومقارنتها بالإنفاق الفعلي
    /// </summary>
    [HttpGet("budgets")]
    [RequireFinancialPermission(FinancialPermissions.ViewDashboard)]
    public async Task<ActionResult<IEnumerable<MonthlyBudgetDto>>> GetBudgets([FromQuery] int year, [FromQuery] int month, [FromQuery] Guid branchId)
    {
        var budgets = await _expenseService.GetMonthlyBudgetsAsync(year, month, branchId);
        return Ok(budgets);
    }

    /// <summary>
    /// ضبط موازنة شهرية لتصنيف محدد وتعيين نسبة التنبيه
    /// </summary>
    [HttpPost("budgets")]
    [RequireFinancialPermission(FinancialPermissions.ManageBudgets)]
    public async Task<ActionResult<MonthlyBudgetDto>> SetBudget([FromBody] SetMonthlyBudgetDto dto)
    {
        var userId = GetCurrentUserId();
        var budget = await _expenseService.SetMonthlyBudgetAsync(dto, userId);
        return Ok(budget);
    }

    /// <summary>
    /// جلب سجل التدقيق والعمليات لمصروف معين
    /// </summary>
    [HttpGet("{id:guid}/audit-logs")]
    [RequireFinancialPermission(FinancialPermissions.ViewDashboard)]
    public async Task<ActionResult<IEnumerable<FinancialAuditLogDto>>> GetAuditLogs(Guid id)
    {
        var logs = await _auditLogService.GetLogsForRecordAsync("Expense", id.ToString());
        return Ok(logs);
    }

    private string GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "SYSTEM_DOCTOR";
    private string GetCurrentUserName() => User.FindFirst(ClaimTypes.Name)?.Value ?? "د. صديق";
}

public class CreateCategoryRequest
{
    public string NameAr { get; set; } = string.Empty;
    public bool IsDirectCost { get; set; }
}
