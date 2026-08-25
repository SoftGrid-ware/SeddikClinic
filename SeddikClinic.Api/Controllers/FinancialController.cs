using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SeddikClinic.Api.Security;
using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Core.Interfaces;

namespace SeddikClinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FinancialController : ControllerBase
{
    private readonly IFinancialReportService _reportService;
    private readonly IFinancialPeriodService _periodService;
    private readonly IFileStorageService _storageService;

    public FinancialController(
        IFinancialReportService reportService,
        IFinancialPeriodService periodService,
        IFileStorageService storageService)
    {
        _reportService = reportService;
        _periodService = periodService;
        _storageService = storageService;
    }

    /// <summary>
    /// جلب مؤشرات وأرقام شاشة الأرباح والمصروفات للطبيب
    /// </summary>
    [HttpGet("dashboard")]
    [RequireFinancialPermission(FinancialPermissions.ViewDashboard, requireReauth: false)]
    public async Task<ActionResult<FinancialDashboardDto>> GetDashboard([FromQuery] FinancialFilterDto filter)
    {
        var metrics = await _reportService.GetDashboardMetricsAsync(filter);
        return Ok(metrics);
    }

    /// <summary>
    /// تصدير كشف المصروفات والتقارير المالية إلى ملف Excel
    /// </summary>
    [HttpGet("export/excel")]
    [RequireFinancialPermission(FinancialPermissions.ViewDashboard)]
    public async Task<IActionResult> ExportExcel([FromQuery] ExpenseFilterDto filter)
    {
        var bytes = await _reportService.ExportExpensesToExcelAsync(filter);
        return File(bytes, "text/csv; charset=utf-8", $"Expenses_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    /// <summary>
    /// تصدير كشف المصروفات إلى PDF
    /// </summary>
    [HttpGet("export/pdf")]
    [RequireFinancialPermission(FinancialPermissions.ViewDashboard)]
    public async Task<IActionResult> ExportPdf([FromQuery] ExpenseFilterDto filter)
    {
        var bytes = await _reportService.ExportExpensesToPdfAsync(filter);
        return File(bytes, "text/html; charset=utf-8", $"ExpensesReport_{DateTime.UtcNow:yyyyMMdd}.html");
    }

    /// <summary>
    /// جلب أو إنشاء الفترة المالية الحالية للفرع
    /// </summary>
    [HttpGet("periods/current")]
    public async Task<ActionResult<FinancialPeriodDto>> GetCurrentPeriod([FromQuery] Guid branchId, [FromQuery] int? year, [FromQuery] int? month)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var m = month ?? DateTime.UtcNow.Month;
        var period = await _periodService.GetOrCreateCurrentPeriodAsync(branchId, y, m);
        return Ok(period);
    }

    /// <summary>
    /// إقفال الفترة المالية (شهرياً أو دورياً) وتثبيت الأرقام المالية
    /// </summary>
    [HttpPost("periods/close")]
    [RequireFinancialPermission(FinancialPermissions.ClosePeriod)]
    public async Task<ActionResult<FinancialPeriodClosingDto>> ClosePeriod([FromBody] ClosePeriodDto dto)
    {
        var userId = GetCurrentUserId();
        var userName = GetCurrentUserName();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var device = Request.Headers.UserAgent.ToString();

        var result = await _periodService.ClosePeriodAsync(dto, userId, userName, ip, device);
        return Ok(result);
    }

    /// <summary>
    /// إعادة فتح الفترة المالية المقفلة (للمدير العام فقط مع تسجيل السبب)
    /// </summary>
    [HttpPost("periods/reopen")]
    [RequireFinancialPermission(FinancialPermissions.ReopenPeriod)]
    public async Task<IActionResult> ReopenPeriod([FromBody] ReopenPeriodDto dto)
    {
        var userId = GetCurrentUserId();
        var userName = GetCurrentUserName();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var device = Request.Headers.UserAgent.ToString();

        var success = await _periodService.ReopenPeriodAsync(dto, userId, userName, ip, device);
        return Ok(new { success, message = "تمت إعادة فتح الفترة المالية بنجاح وتسجيل السبب في سجل التدقيق." });
    }

    /// <summary>
    /// مراقبة استهلاك الخطة السحابية المجانية (Cloudflare R2 Storage Quota)
    /// </summary>
    [HttpGet("storage/quota")]
    public async Task<ActionResult<CloudStorageQuotaSummaryDto>> GetStorageQuota()
    {
        var quota = await _storageService.GetStorageUsageSummaryAsync();
        return Ok(quota);
    }

    private string GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "SYSTEM_DOCTOR";
    private string GetCurrentUserName() => User.FindFirst(ClaimTypes.Name)?.Value ?? "د. صديق";
}
