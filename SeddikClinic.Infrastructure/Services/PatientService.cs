using Microsoft.EntityFrameworkCore;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.Entities.Appointments;
using SeddikClinic.Core.Enums;
using SeddikClinic.Core.Interfaces;
using SeddikClinic.Infrastructure.Data;

namespace SeddikClinic.Infrastructure.Services;

public class PatientService : IPatientService
{
    private readonly SeddikClinicDbContext _dbContext;

    public PatientService(SeddikClinicDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<PatientDto>> SearchPatientsAsync(string? query, int pageIndex = 1, int pageSize = 50)
    {
        var q = _dbContext.Patients
            .Where(p => !p.IsDeleted)
            .Include(p => p.Appointments)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            q = q.Where(p => p.FullName.ToLower().Contains(term) || 
                             p.PhoneNumber.Contains(term) || 
                             p.PatientCode.ToLower().Contains(term));
        }

        var patients = await q.OrderByDescending(p => p.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return patients.Select(MapToDto).ToList();
    }

    public async Task<PatientDto?> GetPatientByIdAsync(Guid patientId)
    {
        var p = await _dbContext.Patients
            .Include(p => p.Appointments)
            .FirstOrDefaultAsync(p => p.Id == patientId);

        return p != null ? MapToDto(p) : null;
    }

    public async Task<PatientDto> CreatePatientAsync(CreatePatientDto dto)
    {
        var count = await _dbContext.Patients.CountAsync();
        var patientCode = $"P-{(count + 1001)}";

        var patient = new Patient
        {
            PatientCode = patientCode,
            FullName = dto.FullName.Trim(),
            PhoneNumber = dto.PhoneNumber.Trim(),
            AlternativePhone = dto.AlternativePhone?.Trim(),
            NationalId = dto.NationalId?.Trim(),
            Gender = dto.Gender ?? "ذكر",
            BirthDate = dto.BirthDate,
            Age = dto.Age,
            Address = dto.Address,
            BloodGroup = dto.BloodGroup,
            MedicalHistory = dto.MedicalHistory,
            Allergies = dto.Allergies,
            Notes = dto.Notes
        };

        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        return MapToDto(patient);
    }

    public async Task<PatientDto> UpdatePatientAsync(Guid patientId, CreatePatientDto dto)
    {
        var p = await _dbContext.Patients.FindAsync(patientId);
        if (p == null) throw new InvalidOperationException("المريض غير موجود.");

        p.FullName = dto.FullName.Trim();
        p.PhoneNumber = dto.PhoneNumber.Trim();
        p.AlternativePhone = dto.AlternativePhone?.Trim();
        p.NationalId = dto.NationalId?.Trim();
        p.Gender = dto.Gender;
        p.BirthDate = dto.BirthDate;
        p.Age = dto.Age;
        p.Address = dto.Address;
        p.BloodGroup = dto.BloodGroup;
        p.MedicalHistory = dto.MedicalHistory;
        p.Allergies = dto.Allergies;
        p.Notes = dto.Notes;

        await _dbContext.SaveChangesAsync();
        return MapToDto(p);
    }

    public async Task<bool> DeletePatientAsync(Guid patientId)
    {
        var p = await _dbContext.Patients
            .Include(x => x.Appointments)
            .FirstOrDefaultAsync(x => x.Id == patientId);
        
        if (p == null) return false;

        p.IsDeleted = true;
        if (p.Appointments != null)
        {
            foreach (var a in p.Appointments)
            {
                a.IsDeleted = true;
            }
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }

    private static PatientDto MapToDto(Patient p)
    {
        var activeAppointments = p.Appointments?
            .Where(a => !a.IsDeleted)
            .OrderByDescending(a => a.AppointmentDate)
            .ToList() ?? new List<Appointment>();

        var lastVisit = activeAppointments.FirstOrDefault()?.AppointmentDate;

        var visits = activeAppointments.Select(a => new PatientVisitHistoryDto
        {
            AppointmentId = a.Id,
            AppointmentDate = a.AppointmentDate,
            TimeFormatted = DateTime.Today.Add(a.StartTime).ToString("hh:mm tt"),
            ServiceType = a.ServiceType,
            TotalFees = a.TotalFees,
            DepositAmount = a.DepositAmount,
            StatusBadge = a.Status switch
            {
                AppointmentStatus.Waiting => "في الانتظار ⏳",
                AppointmentStatus.InProgress => "قيد الكشف 🩺",
                AppointmentStatus.Completed => "تم الكشف ✅",
                AppointmentStatus.Cancelled => "ملغي ❌",
                _ => "مجدول 📅"
            },
            Notes = a.Notes
        }).ToList();

        return new PatientDto
        {
            Id = p.Id,
            PatientCode = p.PatientCode,
            FullName = p.FullName,
            PhoneNumber = p.PhoneNumber,
            AlternativePhone = p.AlternativePhone,
            NationalId = p.NationalId,
            Gender = p.Gender,
            BirthDate = p.BirthDate,
            Age = p.Age,
            Address = p.Address,
            BloodGroup = p.BloodGroup,
            MedicalHistory = p.MedicalHistory,
            Allergies = p.Allergies,
            Notes = p.Notes,
            TotalVisits = activeAppointments.Count,
            LastVisitDate = lastVisit,
            CreatedAt = p.CreatedAt,
            Visits = visits
        };
    }
}
