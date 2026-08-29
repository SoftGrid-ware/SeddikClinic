using SeddikClinic.Core.Entities.Appointments;

namespace SeddikClinic.Core.DTOs.Appointments;

public class DentalToothRecordDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public int ToothNumber { get; set; }
    public ToothCondition Condition { get; set; }
    public string ConditionNameAr => Condition switch
    {
        ToothCondition.Healthy => "سليم",
        ToothCondition.Decayed => "تسوس / نخر",
        ToothCondition.Filled => "حشو",
        ToothCondition.RootCanal => "علاج عصب وجذور",
        ToothCondition.Crown => "طربوش / زيركون",
        ToothCondition.Extracted => "مخلوع / مفقود",
        ToothCondition.Implant => "زراعة سن",
        ToothCondition.Bridge => "جسر / كوبري",
        ToothCondition.Veneer => "فينير / ابتسامة",
        ToothCondition.Impacted => "مدفون / منطمر",
        ToothCondition.Orthodontic => "تقويم",
        _ => "غير محدد"
    };

    public string ConditionColorHex => Condition switch
    {
        ToothCondition.Healthy => "#10B981",    // Green
        ToothCondition.Decayed => "#EF4444",    // Red
        ToothCondition.Filled => "#3B82F6",     // Blue
        ToothCondition.RootCanal => "#8B5CF6",  // Purple
        ToothCondition.Crown => "#F59E0B",      // Amber / Gold
        ToothCondition.Extracted => "#94A3B8",  // Slate / Grey
        ToothCondition.Implant => "#06B6D4",    // Cyan
        ToothCondition.Bridge => "#EC4899",     // Pink
        ToothCondition.Veneer => "#14B8A6",     // Teal
        ToothCondition.Impacted => "#64748B",   // Dark Slate
        ToothCondition.Orthodontic => "#6366F1",// Indigo
        _ => "#E2E8F0"
    };

    public string? AffectedSurfaces { get; set; }
    public string? Notes { get; set; }
    public decimal EstimatedCost { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateToothRecordDto
{
    public Guid PatientId { get; set; }
    public int ToothNumber { get; set; }
    public ToothCondition Condition { get; set; }
    public string? AffectedSurfaces { get; set; }
    public string? Notes { get; set; }
    public decimal EstimatedCost { get; set; }
    public bool IsCompleted { get; set; }
}

public class PatientDentalImageDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DentalImageType ImageType { get; set; }
    public string ImageTypeNameAr => ImageType switch
    {
        DentalImageType.PanoramicXRay => "أشعة بانوراما",
        DentalImageType.PeriapicalXRay => "أشعة موضعية (Periapical)",
        DentalImageType.BitewingXRay => "أشعة إطباقية",
        DentalImageType.Cephalometric => "أشعة سيفالومترية",
        DentalImageType.BeforeTreatment => "صورة قبل العلاج (Before)",
        DentalImageType.AfterTreatment => "صورة بعد العلاج (After)",
        _ => "صورة عامة"
    };
    public string ImageUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? Notes { get; set; }
    public int? AssociatedToothNumber { get; set; }
    public DateTime TakenAt { get; set; }
}

public class CreateDentalImageDto
{
    public Guid PatientId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DentalImageType ImageType { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int? AssociatedToothNumber { get; set; }
}

public class PatientDentalChartSummaryDto
{
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public List<DentalToothRecordDto> Teeth { get; set; } = new();
    public List<PatientDentalImageDto> Images { get; set; } = new();
    public int TotalDecayed { get; set; }
    public int TotalFilled { get; set; }
    public int TotalRootCanal { get; set; }
    public int TotalCrowns { get; set; }
    public int TotalMissing { get; set; }
    public decimal TotalEstimatedTreatmentCost { get; set; }
}
