using SeddikClinic.Core.Entities.Appointments;

namespace SeddikClinic.Core.Entities.Billing;

public class Prescription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PrescriptionNumber { get; set; } = string.Empty;
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public Guid? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    public string DoctorName { get; set; } = "د. صديق";
    public string Diagnosis { get; set; } = string.Empty;
    public string? GeneralInstructions { get; set; }

    public List<PrescriptionItem> Items { get; set; } = new();

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
}

public class PrescriptionItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PrescriptionId { get; set; }
    public Prescription? Prescription { get; set; }

    public string MedicationName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty; // e.g., "1g", "500mg"
    public string Frequency { get; set; } = string.Empty; // e.g., "قرص كل 8 ساعات بعد الأكل"
    public string Duration { get; set; } = string.Empty; // e.g., "5 أيام"
    public string? Instructions { get; set; } // e.g., "مع شرب كمية كافية من الماء"
    public int DisplayOrder { get; set; } = 1;
}

public class DentalDrugCatalogItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TradeName { get; set; } = string.Empty; // اسم الدواء التجاري (e.g., Augmentin, Cataflam)
    public string ScientificName { get; set; } = string.Empty; // الاسم العلمي (e.g., Amoxicillin + Clavulanate)
    public string Category { get; set; } = "مضاد حيوي"; // مضاد حيوي، مسكن، مضاد التهاب، غسول فم
    public string DefaultDosage { get; set; } = "1g";
    public string DefaultFrequency { get; set; } = "قرص كل 12 ساعة بعد الأكل";
    public string DefaultDuration { get; set; } = "5 إلى 7 أيام";
    public string? DefaultInstructions { get; set; }
    public bool IsCommon { get; set; } = true;
}
