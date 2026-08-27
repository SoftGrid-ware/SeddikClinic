using Microsoft.AspNetCore.Mvc;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.Interfaces;

namespace SeddikClinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly IPatientService _patientService;

    public PatientsController(IPatientService patientService)
    {
        _patientService = patientService;
    }

    /// <summary>
    /// البحث في سجلات المرضى
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<PatientDto>>> SearchPatients([FromQuery] string? query, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 50)
    {
        var patients = await _patientService.SearchPatientsAsync(query, pageIndex, pageSize);
        return Ok(patients);
    }

    /// <summary>
    /// جلب تفاصيل مريض
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PatientDto>> GetById(Guid id)
    {
        var patient = await _patientService.GetPatientByIdAsync(id);
        if (patient == null) return NotFound("المريض غير موجود.");
        return Ok(patient);
    }

    /// <summary>
    /// تسجيل مريض جديد
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PatientDto>> CreatePatient([FromBody] CreatePatientDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            return BadRequest("اسم المريض ورقم الهاتف مطلوبان.");
        }

        var patient = await _patientService.CreatePatientAsync(dto);
        return Ok(patient);
    }

    /// <summary>
    /// تعديل بيانات مريض
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<PatientDto>> UpdatePatient(Guid id, [FromBody] CreatePatientDto dto)
    {
        var patient = await _patientService.UpdatePatientAsync(id, dto);
        return Ok(patient);
    }

    /// <summary>
    /// حذف مريض من سجل المرضى
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePatient(Guid id)
    {
        var success = await _patientService.DeletePatientAsync(id);
        if (!success) return NotFound("المريض غير موجود.");
        return Ok(new { success = true, message = "تم حذف المريض من السجل بنجاح." });
    }
}
