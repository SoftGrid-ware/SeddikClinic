using SeddikClinic.Core.DTOs.Appointments;

namespace SeddikClinic.Core.Interfaces;

public interface IClinicServiceCatalogService
{
    Task<List<ClinicServiceDto>> GetAllServicesAsync();
    Task<ClinicServiceDto?> GetServiceByIdAsync(Guid id);
    Task<ClinicServiceDto> CreateServiceAsync(CreateClinicServiceDto dto);
    Task<ClinicServiceDto?> UpdateServiceAsync(Guid id, UpdateClinicServiceDto dto);
    Task<bool> DeleteServiceAsync(Guid id);
    Task<bool> UpdateConsultationPriceAsync(decimal newPrice);
}
