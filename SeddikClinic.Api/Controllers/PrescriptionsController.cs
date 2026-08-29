using Microsoft.AspNetCore.Mvc;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.Interfaces;

namespace SeddikClinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PrescriptionsController : ControllerBase
{
    private readonly IPrescriptionService _prescriptionService;

    public PrescriptionsController(IPrescriptionService prescriptionService)
    {
        _prescriptionService = prescriptionService;
    }

    [HttpGet("patient/{patientId}")]
    public async Task<ActionResult<List<PrescriptionDto>>> GetPatientPrescriptions(Guid patientId)
    {
        var list = await _prescriptionService.GetPatientPrescriptionsAsync(patientId);
        return Ok(list);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PrescriptionDto>> GetPrescriptionById(Guid id)
    {
        var p = await _prescriptionService.GetPrescriptionByIdAsync(id);
        if (p == null) return NotFound("الروشتة غير موجودة.");
        return Ok(p);
    }

    [HttpPost]
    public async Task<ActionResult<PrescriptionDto>> CreatePrescription([FromBody] CreatePrescriptionDto dto)
    {
        var created = await _prescriptionService.CreatePrescriptionAsync(dto);
        return Ok(created);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePrescription(Guid id)
    {
        var success = await _prescriptionService.DeletePrescriptionAsync(id);
        if (!success) return NotFound("الروشتة غير موجودة.");
        return Ok(new { success = true, message = "تم حذف الروشتة بنجاح." });
    }

    [HttpGet("drugs-catalog")]
    public async Task<ActionResult<List<DentalDrugCatalogItemDto>>> GetCommonDrugs()
    {
        var drugs = await _prescriptionService.GetCommonDentalDrugsCatalogAsync();
        return Ok(drugs);
    }
}
