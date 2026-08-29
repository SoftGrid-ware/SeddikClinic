namespace SeddikClinic.Core.Entities.Appointments;

public enum ToothCondition
{
    Healthy = 1,        // سليم
    Decayed = 2,        // تسوس / نخر
    Filled = 3,         // حشو كمبوزيت / أملجم
    RootCanal = 4,      // علاج جذور وعصب
    Crown = 5,          // طربوش / تلبيسة زيركون أو بورسلين
    Extracted = 6,      // مخلوع / مفقود
    Implant = 7,        // زراعة سن
    Bridge = 8,         // كوبري / جسر
    Veneer = 9,         // فينير / ابتسامة هوليوود
    Impacted = 10,      // سن منطمر / مدفون
    Orthodontic = 11    // تقويم
}

public class DentalToothRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    /// <summary>
    /// رقم السن حسب الترقيم العالمي FDI (11-48 للبالغين، 51-85 للأطفال)
    /// </summary>
    public int ToothNumber { get; set; }

    public ToothCondition Condition { get; set; } = ToothCondition.Healthy;

    /// <summary>
    /// الأسطح المتأثرة (Mesial, Distal, Occlusal, Buccal, Lingual) مثل: "MOD", "B", "O"
    /// </summary>
    public string? AffectedSurfaces { get; set; }

    public string? Notes { get; set; }
    public decimal EstimatedCost { get; set; } = 0;
    public bool IsCompleted { get; set; } = false;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum DentalImageType
{
    PanoramicXRay = 1,      // أشعة بانوراما
    PeriapicalXRay = 2,     // أشعة سينية موضعية
    BitewingXRay = 3,       // أشعة إطباقية
    Cephalometric = 4,      // أشعة سيفالومترية
    BeforeTreatment = 5,    // صورة قبل العلاج
    AfterTreatment = 6,     // صورة بعد العلاج
    General = 7             // صورة عامة
}

public class PatientDentalImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public string Title { get; set; } = string.Empty;
    public DentalImageType ImageType { get; set; } = DentalImageType.General;
    public string ImageUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? Notes { get; set; }
    public int? AssociatedToothNumber { get; set; }

    public DateTime TakenAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
