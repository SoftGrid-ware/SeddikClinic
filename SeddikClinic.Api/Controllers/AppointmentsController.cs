using Microsoft.AspNetCore.Mvc;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.Enums;
using SeddikClinic.Core.Interfaces;

namespace SeddikClinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;

    public AppointmentsController(IAppointmentService appointmentService)
    {
        _appointmentService = appointmentService;
    }

    /// <summary>
    /// جلب ملخص وجدول مواعيد اليوم
    /// </summary>
    [HttpGet("today")]
    public async Task<ActionResult<AppointmentSummaryDto>> GetTodaySummary([FromQuery] Guid? doctorId, [FromQuery] Guid? branchId)
    {
        var summary = await _appointmentService.GetTodayAppointmentsSummaryAsync(doctorId, branchId);
        return Ok(summary);
    }

    /// <summary>
    /// جلب وبحث المواعيد بالتاريخ أو الحالة
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<AppointmentDto>>> GetAppointments(
        [FromQuery] DateTime? date,
        [FromQuery] Guid? doctorId,
        [FromQuery] AppointmentStatus? status,
        [FromQuery] string? searchTerm,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var appointments = await _appointmentService.GetAppointmentsAsync(date, doctorId, status, searchTerm, startDate, endDate);
        return Ok(appointments);
    }

    /// <summary>
    /// حجز موعد جديد
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AppointmentDto>> CreateAppointment([FromBody] CreateAppointmentDto dto)
    {
        var userName = User.Identity?.Name ?? "الاستقبال";
        var appointment = await _appointmentService.CreateAppointmentAsync(dto, userName);
        return Ok(appointment);
    }

    /// <summary>
    /// تحديث حالة الموعد (في الانتظار، داخل العيادة، تم الكشف، ملغي)
    /// </summary>
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateAppointmentStatusDto dto)
    {
        var success = await _appointmentService.UpdateAppointmentStatusAsync(id, dto.Status, dto.CancellationReason);
        if (!success) return NotFound("الموعد غير موجود.");
        return Ok(new { success = true, message = "تم تحديث حالة الموعد بنجاح." });
    }

    /// <summary>
    /// إلغاء الموعد
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] UpdateAppointmentStatusDto dto)
    {
        var success = await _appointmentService.CancelAppointmentAsync(id, dto.CancellationReason ?? "تم الإلغاء");
        if (!success) return NotFound("الموعد غير موجود.");
        return Ok(new { success = true, message = "تم إلغاء الموعد بنجاح." });
    }

    /// <summary>
    /// حذف الموعد نهائياً
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await _appointmentService.DeleteAppointmentAsync(id);
        if (!success) return NotFound("الموعد غير موجود.");
        return Ok(new { success = true, message = "تم حذف الموعد بنجاح." });
    }

    /// <summary>
    /// تعديل الخدمة الطبية والرسوم للموعد
    /// </summary>
    [HttpPut("{id}/service")]
    public async Task<IActionResult> UpdateService(Guid id, [FromBody] UpdateAppointmentServiceDto dto)
    {
        var success = await _appointmentService.UpdateAppointmentServiceAsync(id, dto.ServiceType, dto.TotalFees);
        if (!success) return NotFound("الموعد غير موجود.");
        return Ok(new { success = true, message = "تم تعديل الخدمة الطبية بنجاح." });
    }

    /// <summary>
    /// تعديل موعد وتاريخ الحجز (إعادة جدولة)
    /// </summary>
    [HttpPut("{id}/reschedule")]
    public async Task<IActionResult> Reschedule(Guid id, [FromBody] RescheduleAppointmentDto dto)
    {
        var success = await _appointmentService.RescheduleAppointmentAsync(id, dto.NewDate, dto.NewStartTime, dto.DurationMinutes);
        if (!success) return NotFound("الموعد غير موجود.");
        return Ok(new { success = true, message = "تم تعديل موعد الحجز بنجاح." });
    }

    /// <summary>
    /// تعديل البيانات المالية والعربون للموعد
    /// </summary>
    [HttpPut("{id}/financials")]
    public async Task<IActionResult> UpdateFinancials(Guid id, [FromBody] UpdateAppointmentFinancialsDto dto)
    {
        var success = await _appointmentService.UpdateAppointmentFinancialsAsync(id, dto.TotalFees, dto.DepositAmount, dto.IsDepositPaid);
        if (!success) return NotFound("الموعد غير موجود.");
        return Ok(new { success = true, message = "تم تحديث البيانات المالية بنجاح." });
    }

    /// <summary>
    /// دفع دفعة أو قسط من رسوم الكشف
    /// </summary>
    [HttpPost("{id}/pay-installment")]
    public async Task<IActionResult> PayInstallment(Guid id, [FromBody] PayInstallmentDto dto)
    {
        if (dto.Amount <= 0) return BadRequest("المبلغ يجب أن يكون أكبر من الصفر.");
        var success = await _appointmentService.RecordInstallmentPaymentAsync(id, dto.Amount);
        if (!success) return NotFound("الموعد غير موجود.");
        return Ok(new { success = true, message = "تم تسجيل وتحصيل القسط بنجاح." });
    }
}

public class UpdateAppointmentFinancialsDto
{
    public decimal? TotalFees { get; set; }
    public decimal? DepositAmount { get; set; }
    public bool? IsDepositPaid { get; set; }
}

public class PayInstallmentDto
{
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
}
