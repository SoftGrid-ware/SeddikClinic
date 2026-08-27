namespace SeddikClinic.Mobile.Shared.Models;

public static class PatientSession
{
    public static Guid? PatientId { get; set; }
    public static string? PatientName { get; set; }
    public static string? FullName { get => PatientName; set => PatientName = value; }
    public static string? PhoneNumber { get; set; }
    public static string? PatientCode { get; set; }
    public static int? Age { get; set; }
    public static string? BloodGroup { get; set; }
    public static string? Allergies { get; set; }
    public static string? MedicalHistory { get; set; }

    public static bool IsLoggedIn => PatientId.HasValue && !string.IsNullOrEmpty(PhoneNumber);

    public static void Clear()
    {
        PatientId = null;
        PatientName = null;
        PhoneNumber = null;
        PatientCode = null;
        Age = null;
        BloodGroup = null;
        Allergies = null;
        MedicalHistory = null;
    }
}
