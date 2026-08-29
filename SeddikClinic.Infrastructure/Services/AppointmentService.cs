using Microsoft.EntityFrameworkCore;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.Entities.Appointments;
using SeddikClinic.Core.Enums;
using SeddikClinic.Core.Interfaces;
using SeddikClinic.Infrastructure.Data;

namespace SeddikClinic.Infrastructure.Services;

public class AppointmentService : IAppointmentService
{
    private readonly SeddikClinicDbContext _dbContext;
    private readonly IPatientService _patientService;

    public AppointmentService(SeddikClinicDbContext dbContext, IPatientService patientService)
    {
        _dbContext = dbContext;
        _patientService = patientService;
    }

    private static TimeSpan ParseTimeSlot(string? timeStr, TimeSpan fallbackTime)
    {
        if (string.IsNullOrWhiteSpace(timeStr)) return fallbackTime;

        if (TimeSpan.TryParse(timeStr, out var ts))
            return ts;

        var cleanStr = timeStr.Trim().Replace("م", "PM").Replace("ص", "AM");
        if (DateTime.TryParse(cleanStr, System.Globalization.CultureInfo.InvariantCulture, out var dt))
            return dt.TimeOfDay;

        string[] formats = { "hh:mm tt", "h:mm tt", "hh:mm:ss tt", "h:mm:ss tt", "HH:mm", "H:mm", "hh:mm", "h:mm" };
        if (DateTime.TryParseExact(cleanStr, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
            return dt.TimeOfDay;

        return fallbackTime;
    }

    public async Task<AppointmentSummaryDto> GetTodayAppointmentsSummaryAsync(Guid? doctorId = null, Guid? branchId = null)
    {
        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);

        var query = _dbContext.Appointments
            .Include(a => a.Patient)
            .Where(a => !a.IsDeleted && a.AppointmentDate >= todayStart && a.AppointmentDate < todayEnd);

        if (doctorId.HasValue) query = query.Where(a => a.DoctorId == doctorId.Value);
        if (branchId.HasValue) query = query.Where(a => a.BranchId == branchId.Value);

        var appointments = await query.ToListAsync();
        var orderedAppointments = appointments.OrderBy(a => a.StartTime).ToList();

        var dtos = orderedAppointments.Select(MapToDto).ToList();

        return new AppointmentSummaryDto
        {
            TotalToday = appointments.Count,
            WaitingCount = appointments.Count(a => a.Status == AppointmentStatus.Waiting),
            InProgressCount = appointments.Count(a => a.Status == AppointmentStatus.InProgress),
            CompletedCount = appointments.Count(a => a.Status == AppointmentStatus.Completed),
            CancelledCount = appointments.Count(a => a.Status == AppointmentStatus.Cancelled),
            TodayAppointments = dtos
        };
    }

    public async Task<List<AppointmentDto>> GetAppointmentsAsync(DateTime? date, Guid? doctorId = null, AppointmentStatus? status = null, string? searchTerm = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbContext.Appointments
            .Include(a => a.Patient)
            .Where(a => !a.IsDeleted)
            .AsQueryable();

        if (startDate.HasValue && endDate.HasValue)
        {
            var s = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
            var e = DateTime.SpecifyKind(endDate.Value.Date, DateTimeKind.Utc).AddDays(1);
            query = query.Where(a => a.AppointmentDate >= s && a.AppointmentDate < e);
        }
        else if (date.HasValue)
        {
            var d = DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Utc);
            var nextD = d.AddDays(1);
            query = query.Where(a => a.AppointmentDate >= d && a.AppointmentDate < nextD);
        }

        if (doctorId.HasValue) query = query.Where(a => a.DoctorId == doctorId.Value);
        if (status.HasValue) query = query.Where(a => a.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(a => a.Patient!.FullName.ToLower().Contains(term) || 
                                     a.Patient.PhoneNumber.Contains(term) || 
                                     a.AppointmentNumber.ToLower().Contains(term));
        }

        var list = await query.OrderByDescending(a => a.AppointmentDate).ToListAsync();
        var orderedList = list.OrderByDescending(a => a.AppointmentDate.Date).ThenBy(a => a.StartTime).ToList();
        return orderedList.Select(MapToDto).ToList();
    }

    public async Task<AppointmentDto> CreateAppointmentAsync(CreateAppointmentDto dto, string createdByUserName)
    {
        Guid patientId;

        // إذا كان المريض مسجل مسبقاً
        if (dto.PatientId.HasValue && dto.PatientId != Guid.Empty)
        {
            patientId = dto.PatientId.Value;
        }
        else
        {
            // البحث عن مريض مسجل مسبقاً بنفس رقم الهاتف لتفادي التكرار
            var cleanPhone = dto.NewPatientPhone?.Trim();
            var existingPatient = !string.IsNullOrWhiteSpace(cleanPhone)
                ? await _dbContext.Patients.FirstOrDefaultAsync(p => !p.IsDeleted && p.PhoneNumber == cleanPhone)
                : null;

            if (existingPatient != null)
            {
                patientId = existingPatient.Id;
                if (!string.IsNullOrWhiteSpace(dto.NewPatientFullName) && (existingPatient.FullName == "مريض غير مسجل" || string.IsNullOrWhiteSpace(existingPatient.FullName)))
                {
                    existingPatient.FullName = dto.NewPatientFullName.Trim();
                    await _dbContext.SaveChangesAsync();
                }
            }
            else
            {
                // إنشاء مريض جديد فوراً
                var newPatient = await _patientService.CreatePatientAsync(new CreatePatientDto
                {
                    FullName = dto.NewPatientFullName ?? "مريض غير مسجل",
                    PhoneNumber = dto.NewPatientPhone ?? "01000000000"
                });
                patientId = newPatient.Id;
            }
        }

        var startTime = ParseTimeSlot(dto.StartTimeString, dto.StartTime != TimeSpan.Zero ? dto.StartTime : new TimeSpan(17, 0, 0));
        var endTime = startTime.Add(TimeSpan.FromMinutes(dto.DurationMinutes > 0 ? dto.DurationMinutes : 30));
        var aptDate = DateTime.SpecifyKind(dto.AppointmentDate.Date, DateTimeKind.Utc);
        var nextAptDate = aptDate.AddDays(1);
        var count = await _dbContext.Appointments.CountAsync(a => a.AppointmentDate >= aptDate && a.AppointmentDate < nextAptDate);
        var aptNumber = $"APT-{aptDate:yyyyMMdd}-{(count + 1):D3}";

        var appointment = new Appointment
        {
            AppointmentNumber = aptNumber,
            PatientId = patientId,
            DoctorId = dto.DoctorId,
            DoctorName = !string.IsNullOrWhiteSpace(dto.DoctorName) ? dto.DoctorName : "د. صديق",
            AppointmentDate = aptDate,
            StartTime = startTime,
            EndTime = endTime,
            ServiceType = dto.ServiceType,
            ReasonForVisit = !string.IsNullOrWhiteSpace(dto.ReasonForVisit) ? dto.ReasonForVisit : dto.Notes,
            Status = AppointmentStatus.Scheduled,
            TotalFees = dto.TotalFees,
            DiscountAmount = dto.DiscountAmount,
            DepositAmount = dto.DepositAmount,
            IsDepositPaid = dto.DepositAmount > 0,
            Notes = !string.IsNullOrWhiteSpace(dto.Notes) ? dto.Notes : dto.ReasonForVisit,
            CreatedByUserName = createdByUserName
        };

        _dbContext.Appointments.Add(appointment);

        // تحديث السجل الطبي والحساسية في ملف المريض تلقائياً
        var patientToUpdate = await _dbContext.Patients.FindAsync(patientId);
        if (patientToUpdate != null)
        {
            var noteContent = dto.ReasonForVisit ?? dto.Notes ?? "";
            if (noteContent.Contains("[السجل الصحي:") || noteContent.Contains("حساسية") || noteContent.Contains("ضغط") || noteContent.Contains("سكري"))
            {
                patientToUpdate.MedicalHistory = noteContent;
                if (noteContent.Contains("حساسية"))
                {
                    patientToUpdate.Allergies = noteContent;
                }
            }
        }

        await _dbContext.SaveChangesAsync();

        return MapToDto(appointment);
    }

    public async Task<bool> UpdateAppointmentStatusAsync(Guid appointmentId, AppointmentStatus newStatus, string? cancellationReason = null)
    {
        var apt = await _dbContext.Appointments.FindAsync(appointmentId);
        if (apt == null) return false;

        apt.Status = newStatus;
        if (!string.IsNullOrEmpty(cancellationReason))
        {
            apt.CancellationReason = cancellationReason;
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CancelAppointmentAsync(Guid appointmentId, string reason)
    {
        return await UpdateAppointmentStatusAsync(appointmentId, AppointmentStatus.Cancelled, reason);
    }

    public async Task<bool> DeleteAppointmentAsync(Guid appointmentId)
    {
        var apt = await _dbContext.Appointments.FindAsync(appointmentId);
        if (apt == null) return false;

        apt.IsDeleted = true;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateAppointmentServiceAsync(Guid appointmentId, string serviceType, decimal? newFees)
    {
        var apt = await _dbContext.Appointments.FindAsync(appointmentId);
        if (apt == null) return false;

        apt.ServiceType = serviceType;
        if (newFees.HasValue)
        {
            apt.TotalFees = newFees.Value;
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RescheduleAppointmentAsync(Guid appointmentId, DateTime newDate, string newStartTime, int durationMinutes = 30)
    {
        var apt = await _dbContext.Appointments.FindAsync(appointmentId);
        if (apt == null) return false;

        apt.AppointmentDate = DateTime.SpecifyKind(newDate.Date, DateTimeKind.Utc);
        var parsedTime = ParseTimeSlot(newStartTime, apt.StartTime);
        apt.StartTime = parsedTime;
        apt.EndTime = parsedTime.Add(TimeSpan.FromMinutes(durationMinutes > 0 ? durationMinutes : 30));

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateAppointmentFinancialsAsync(Guid appointmentId, decimal? totalFees, decimal? depositAmount, bool? isDepositPaid, decimal? discountAmount = null)
    {
        var apt = await _dbContext.Appointments.FindAsync(appointmentId);
        if (apt == null) return false;

        if (totalFees.HasValue) apt.TotalFees = totalFees.Value;
        if (discountAmount.HasValue) apt.DiscountAmount = discountAmount.Value;
        if (depositAmount.HasValue) apt.DepositAmount = depositAmount.Value;
        if (isDepositPaid.HasValue) apt.IsDepositPaid = isDepositPaid.Value;

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RecordInstallmentPaymentAsync(Guid appointmentId, decimal paymentAmount)
    {
        var apt = await _dbContext.Appointments.FindAsync(appointmentId);
        if (apt == null) return false;

        apt.DepositAmount += paymentAmount;
        apt.IsDepositPaid = true;
        var netFees = Math.Max(0, apt.TotalFees - apt.DiscountAmount);
        if (apt.DepositAmount > netFees)
        {
            apt.DepositAmount = netFees;
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }

    private static AppointmentDto MapToDto(Appointment a)
    {
        var startStr = DateTime.Today.Add(a.StartTime).ToString("hh:mm tt");
        var endStr = DateTime.Today.Add(a.EndTime).ToString("hh:mm tt");

        return new AppointmentDto
        {
            Id = a.Id,
            AppointmentNumber = a.AppointmentNumber,
            PatientId = a.PatientId,
            PatientName = a.Patient?.FullName ?? "غير معروف",
            PatientPhone = a.Patient?.PhoneNumber ?? "",
            DoctorName = a.DoctorName,
            AppointmentDate = a.AppointmentDate,
            StartTime = a.StartTime,
            EndTime = a.EndTime,
            FormattedTime = $"{startStr} - {endStr}",
            ServiceType = a.ServiceType,
            Status = a.Status,
            TotalFees = a.TotalFees,
            DiscountAmount = a.DiscountAmount,
            DepositAmount = a.DepositAmount,
            IsDepositPaid = a.IsDepositPaid,
            Notes = a.Notes,
            ReasonForVisit = a.ReasonForVisit,
            CancellationReason = a.CancellationReason
        };
    }
}
