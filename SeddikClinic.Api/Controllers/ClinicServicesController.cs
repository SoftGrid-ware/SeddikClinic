using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.Interfaces;

namespace SeddikClinic.Api.Controllers;

[ApiController]
[Route("api/clinic-services")]
public class ClinicServicesController : ControllerBase
{
    private readonly IClinicServiceCatalogService _catalogService;

    public ClinicServicesController(IClinicServiceCatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    /// <summary>
    /// جلب قائمة جميع الخدمات والأسعار المتاحة في العيادة
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ClinicServiceDto>>> GetAll()
    {
        var services = await _catalogService.GetAllServicesAsync();
        return Ok(services);
    }

    /// <summary>
    /// جلب خدمة معينة بواسطة المعرف
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ClinicServiceDto>> GetById(Guid id)
    {
        var service = await _catalogService.GetServiceByIdAsync(id);
        if (service == null) return NotFound("الخدمة غير موجودة.");
        return Ok(service);
    }

    /// <summary>
    /// إضافة خدمة طبية جديدة وتحديد سعرها (للمدير)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ClinicServiceDto>> Create([FromBody] CreateClinicServiceDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest("اسم الخدمة مطلوب.");
        }

        var created = await _catalogService.CreateServiceAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// تعديل بيانات وسعر خدمة طبية (للمدير)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ClinicServiceDto>> Update(Guid id, [FromBody] UpdateClinicServiceDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest("اسم الخدمة مطلوب.");
        }

        var updated = await _catalogService.UpdateServiceAsync(id, dto);
        if (updated == null) return NotFound("الخدمة غير موجودة.");
        return Ok(updated);
    }

    /// <summary>
    /// حذف خدمة طبية من قائمة العيادة (للمدير)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await _catalogService.DeleteServiceAsync(id);
        if (!success) return NotFound("الخدمة غير موجودة.");
        return Ok(new { success = true, message = "تم حذف الخدمة بنجاح." });
    }

    /// <summary>
    /// تحديث سريع لسعر الكشف والاستشارة (للمدير)
    /// </summary>
    [HttpPut("consultation-price")]
    public async Task<IActionResult> UpdateConsultationPrice([FromBody] decimal newPrice)
    {
        if (newPrice <= 0) return BadRequest("يرجى إدخال سعر صحيح للكشف.");
        var success = await _catalogService.UpdateConsultationPriceAsync(newPrice);
        return Ok(new { success, message = $"تم تحديث سعر الكشف إلى {newPrice:N0} ج.م بنجاح." });
    }
}
