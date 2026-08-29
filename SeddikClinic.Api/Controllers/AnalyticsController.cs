using Microsoft.AspNetCore.Mvc;
using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Core.Interfaces;

namespace SeddikClinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IClinicAnalyticsService _analyticsService;

    public AnalyticsController(IClinicAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<ClinicAnalyticsOverviewDto>> GetOverview([FromQuery] int monthsBack = 6)
    {
        var overview = await _analyticsService.GetClinicAnalyticsOverviewAsync(monthsBack);
        return Ok(overview);
    }

    [HttpPost("ai-diagnosis")]
    public async Task<ActionResult<PatientAiDiagnosisResultDto>> GetAiDiagnosis([FromBody] PatientAiDiagnosisRequestDto request)
    {
        var result = await _analyticsService.GetAiDiagnosticRecommendationsAsync(request);
        return Ok(result);
    }
}
