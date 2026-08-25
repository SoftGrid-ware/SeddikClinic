namespace SeddikClinic.Core.DTOs.Financial;

public class FileUploadResultDto
{
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CloudStorageQuotaSummaryDto
{
    public long TotalUsedBytes { get; set; }
    public long MaxFreeTierBytes { get; set; } = 10L * 1024 * 1024 * 1024; // 10 GB Free Tier (Cloudflare R2)
    public double UsedPercentage => MaxFreeTierBytes > 0 ? Math.Round(((double)TotalUsedBytes / MaxFreeTierBytes) * 100, 2) : 0;
    public string FormattedUsedSize => $"{Math.Round((double)TotalUsedBytes / (1024 * 1024), 2)} MB";
    public string FormattedMaxSize => $"{MaxFreeTierBytes / (1024 * 1024 * 1024)} GB";
    public bool IsApproachingLimit => UsedPercentage >= 80.0;
    public string AlertMessageAr => IsApproachingLimit
        ? $"تنبيه: تم استهلاك {UsedPercentage}% من السعة التخزينية السحابية المجانية المتاحة ({FormattedUsedSize} من {FormattedMaxSize})."
        : "المساحة التخزينية السحابية ضمن الحدود المجانية الآمنة.";
}
