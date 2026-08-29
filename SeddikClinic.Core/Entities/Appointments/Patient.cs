using SeddikClinic.Core.Entities.Billing;

namespace SeddikClinic.Core.Entities.Appointments;

public class Patient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PatientCode { get; set; } = string.Empty; // كود المريض مثلا P-1001
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? AlternativePhone { get; set; }
    public string? NationalId { get; set; }
    public string? Gender { get; set; } // ذكر / أنثى
    public DateTime? BirthDate { get; set; }
    public int? Age { get; set; }
    public string? Address { get; set; }
    public string? BloodGroup { get; set; }
    public string? MedicalHistory { get; set; } // أمراض مزمنة، ضغط، سكري
    public string? Allergies { get; set; } // حساسية البنسلين، البنج، إلخ
    public string? Notes { get; set; }
    public string? PasswordHash { get; set; } // كلمة مرور المريض المشفرة
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;

    // العلاقات
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<PatientInvoice> Invoices { get; set; } = new List<PatientInvoice>();
}
