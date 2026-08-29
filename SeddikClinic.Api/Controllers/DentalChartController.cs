using Microsoft.AspNetCore.Mvc;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.Entities.Appointments;
using SeddikClinic.Core.Interfaces;

namespace SeddikClinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DentalChartController : ControllerBase
{
    private readonly IDentalChartService _dentalChartService;

    public DentalChartController(IDentalChartService dentalChartService)
    {
        _dentalChartService = dentalChartService;
    }

    [HttpGet("patient/{patientId}")]
    public async Task<ActionResult<PatientDentalChartSummaryDto>> GetPatientChart(Guid patientId)
    {
        var chart = await _dentalChartService.GetPatientDentalChartAsync(patientId);
        return Ok(chart);
    }

    [HttpPost("tooth")]
    public async Task<ActionResult<DentalToothRecordDto>> UpdateToothRecord([FromBody] UpdateToothRecordDto dto)
    {
        var updated = await _dentalChartService.UpdateToothRecordAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("patient/{patientId}/reset")]
    public async Task<IActionResult> ResetPatientTeeth(Guid patientId)
    {
        var success = await _dentalChartService.ResetPatientTeethAsync(patientId);
        return Ok(new { success });
    }

    [HttpGet("patient/{patientId}/images")]
    public async Task<ActionResult<List<PatientDentalImageDto>>> GetPatientImages(Guid patientId, [FromQuery] DentalImageType? type)
    {
        var images = await _dentalChartService.GetPatientImagesAsync(patientId, type);
        return Ok(images);
    }

    [HttpPost("images")]
    public async Task<ActionResult<PatientDentalImageDto>> AddPatientImage([FromBody] CreateDentalImageDto dto)
    {
        var image = await _dentalChartService.AddPatientImageAsync(dto);
        return Ok(image);
    }

    [HttpDelete("images/{imageId}")]
    public async Task<IActionResult> DeletePatientImage(Guid imageId)
    {
        var success = await _dentalChartService.DeletePatientImageAsync(imageId);
        return Ok(new { success });
    }
}
