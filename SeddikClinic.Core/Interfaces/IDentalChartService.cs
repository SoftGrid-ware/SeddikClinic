using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.Entities.Appointments;

namespace SeddikClinic.Core.Interfaces;

public interface IDentalChartService
{
    Task<PatientDentalChartSummaryDto> GetPatientDentalChartAsync(Guid patientId);
    Task<DentalToothRecordDto> UpdateToothRecordAsync(UpdateToothRecordDto dto);
    Task<bool> ResetPatientTeethAsync(Guid patientId);
    Task<List<PatientDentalImageDto>> GetPatientImagesAsync(Guid patientId, DentalImageType? type = null);
    Task<PatientDentalImageDto> AddPatientImageAsync(CreateDentalImageDto dto);
    Task<bool> DeletePatientImageAsync(Guid imageId);
}
