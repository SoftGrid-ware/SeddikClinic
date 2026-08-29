namespace SeddikClinic.Core.DTOs.Appointments;

public class PatientDto
{
    public Guid Id { get; set; }
    public string PatientCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? AlternativePhone { get; set; }
    public string? NationalId { get; set; }
    public string? Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    public int? Age { get; set; }
    public string? Address { get; set; }
    public string? BloodGroup { get; set; }
    public string? MedicalHistory { get; set; }
    public string? Allergies { get; set; }
    public string? Notes { get; set; }
    public bool HasPassword { get; set; }
    public int TotalVisits { get; set; }
    public DateTime? LastVisitDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PatientVisitHistoryDto> Visits { get; set; } = new();
}

public class PatientVisitHistoryDto
{
    public Guid AppointmentId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string DateFormatted => AppointmentDate.ToString("yyyy/MM/dd");
    public string TimeFormatted { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public decimal TotalFees { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal RemainingAmount => Math.Max(0, TotalFees - DepositAmount);
    public string StatusBadge { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class CreatePatientDto
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? AlternativePhone { get; set; }
    public string? NationalId { get; set; }
    public string? Gender { get; set; } = "ذكر";
    public DateTime? BirthDate { get; set; }
    public int? Age { get; set; }
    public string? Address { get; set; }
    public string? BloodGroup { get; set; }
    public string? MedicalHistory { get; set; }
    public string? Allergies { get; set; }
    public string? Notes { get; set; }
}
