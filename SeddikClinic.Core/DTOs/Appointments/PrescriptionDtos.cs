namespace SeddikClinic.Core.DTOs.Appointments;

public class PrescriptionDto
{
    public Guid Id { get; set; }
    public string PrescriptionNumber { get; set; } = string.Empty;
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientPhone { get; set; } = string.Empty;
    public int? PatientAge { get; set; }
    public Guid? AppointmentId { get; set; }
    public string DoctorName { get; set; } = "د. صديق";
    public string Diagnosis { get; set; } = string.Empty;
    public string? GeneralInstructions { get; set; }
    public List<PrescriptionItemDto> Items { get; set; } = new();
    public DateTime IssuedAt { get; set; }
    public string FormattedDate => IssuedAt.ToString("yyyy/MM/dd - hh:mm tt");
}

public class PrescriptionItemDto
{
    public Guid Id { get; set; }
    public string MedicationName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreatePrescriptionDto
{
    public Guid PatientId { get; set; }
    public Guid? AppointmentId { get; set; }
    public string DoctorName { get; set; } = "د. صديق";
    public string Diagnosis { get; set; } = string.Empty;
    public string? GeneralInstructions { get; set; }
    public List<CreatePrescriptionItemDto> Items { get; set; } = new();
}

public class CreatePrescriptionItemDto
{
    public string MedicationName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string? Instructions { get; set; }
}

public class DentalDrugCatalogItemDto
{
    public Guid Id { get; set; }
    public string TradeName { get; set; } = string.Empty;
    public string ScientificName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DefaultDosage { get; set; } = string.Empty;
    public string DefaultFrequency { get; set; } = string.Empty;
    public string DefaultDuration { get; set; } = string.Empty;
    public string? DefaultInstructions { get; set; }
}
