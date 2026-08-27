using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.Enums;

namespace SeddikClinic.Core.Interfaces;

public interface IAppointmentService
{
    Task<AppointmentSummaryDto> GetTodayAppointmentsSummaryAsync(Guid? doctorId = null, Guid? branchId = null);
    Task<List<AppointmentDto>> GetAppointmentsAsync(DateTime? date, Guid? doctorId = null, AppointmentStatus? status = null, string? searchTerm = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<AppointmentDto> CreateAppointmentAsync(CreateAppointmentDto dto, string createdByUserName);
    Task<bool> UpdateAppointmentStatusAsync(Guid appointmentId, AppointmentStatus newStatus, string? cancellationReason = null);
    Task<bool> CancelAppointmentAsync(Guid appointmentId, string reason);
    Task<bool> DeleteAppointmentAsync(Guid appointmentId);
    Task<bool> UpdateAppointmentServiceAsync(Guid appointmentId, string serviceType, decimal? newFees);
    Task<bool> RescheduleAppointmentAsync(Guid appointmentId, DateTime newDate, string newStartTime, int durationMinutes = 30);
    Task<bool> UpdateAppointmentFinancialsAsync(Guid appointmentId, decimal? totalFees, decimal? depositAmount, bool? isDepositPaid);
    Task<bool> RecordInstallmentPaymentAsync(Guid appointmentId, decimal paymentAmount);
}

public interface IPatientService
{
    Task<List<PatientDto>> SearchPatientsAsync(string? query, int pageIndex = 1, int pageSize = 50);
    Task<PatientDto?> GetPatientByIdAsync(Guid patientId);
    Task<PatientDto> CreatePatientAsync(CreatePatientDto dto);
    Task<PatientDto> UpdatePatientAsync(Guid patientId, CreatePatientDto dto);
    Task<bool> DeletePatientAsync(Guid patientId);
}
