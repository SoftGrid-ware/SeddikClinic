using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Core.Interfaces;

namespace SeddikClinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShiftsController : ControllerBase
{
    private readonly IDailyShiftService _shiftService;

    public ShiftsController(IDailyShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    /// <summary>
    /// جلب تفاصيل وحالة الوردية الحالية
    /// </summary>
    [HttpGet("current")]
    public async Task<ActionResult<DailyShiftSummaryDto>> GetCurrentShift([FromQuery] Guid? branchId)
    {
        var shift = await _shiftService.GetCurrentShiftAsync(branchId);
        return Ok(shift);
    }

    /// <summary>
    /// فتح وردية عمل جديدة
    /// </summary>
    [HttpPost("open")]
    public async Task<ActionResult<DailyShiftSummaryDto>> OpenShift([FromBody] OpenShiftRequestDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin-user";
        var userName = User.FindFirstValue(ClaimTypes.Name) ?? "المسؤول";

        try
        {
            var result = await _shiftService.OpenShiftAsync(dto, userId, userName);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// إغلاق وتقفيل الوردية اليومية وجرد الصندوق
    /// </summary>
    [HttpPost("close")]
    public async Task<ActionResult<DailyShiftSummaryDto>> CloseShift([FromBody] CloseShiftRequestDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin-user";
        var userName = User.FindFirstValue(ClaimTypes.Name) ?? "المسؤول";

        try
        {
            var result = await _shiftService.CloseShiftAsync(dto, userId, userName);
            return Ok(result);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// جلب سجل الورديات المقفلة والسابقة
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<List<DailyShiftSummaryDto>>> GetShiftHistory(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] Guid? branchId)
    {
        var history = await _shiftService.GetShiftHistoryAsync(fromDate, toDate, branchId);
        return Ok(history);
    }

    /// <summary>
    /// إعادة فتح وردية مقفلة
    /// </summary>
    [HttpPost("{id}/reopen")]
    public async Task<ActionResult<DailyShiftSummaryDto>> ReopenShift(
        [FromRoute] Guid id,
        [FromBody] ReopenShiftRequestDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin-user";
        var userName = User.FindFirstValue(ClaimTypes.Name) ?? "المسؤول";

        try
        {
            var result = await _shiftService.ReopenShiftAsync(id, userId, userName, dto.Reason);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public class ReopenShiftRequestDto
{
    public string Reason { get; set; } = string.Empty;
}
