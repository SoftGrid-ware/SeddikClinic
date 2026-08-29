using SeddikClinic.Core.DTOs.Financial;

namespace SeddikClinic.Core.Interfaces;

public interface IClinicAnalyticsService
{
    Task<ClinicAnalyticsOverviewDto> GetClinicAnalyticsOverviewAsync(int monthsBack = 6);
    Task<PatientAiDiagnosisResultDto> GetAiDiagnosticRecommendationsAsync(PatientAiDiagnosisRequestDto request);
}
