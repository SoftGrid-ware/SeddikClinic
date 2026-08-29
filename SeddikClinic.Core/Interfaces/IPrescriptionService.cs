using SeddikClinic.Core.DTOs.Appointments;

namespace SeddikClinic.Core.Interfaces;

public interface IPrescriptionService
{
    Task<List<PrescriptionDto>> GetPatientPrescriptionsAsync(Guid patientId);
    Task<PrescriptionDto?> GetPrescriptionByIdAsync(Guid prescriptionId);
    Task<PrescriptionDto> CreatePrescriptionAsync(CreatePrescriptionDto dto);
    Task<bool> DeletePrescriptionAsync(Guid prescriptionId);
    Task<List<DentalDrugCatalogItemDto>> GetCommonDentalDrugsCatalogAsync();
}
