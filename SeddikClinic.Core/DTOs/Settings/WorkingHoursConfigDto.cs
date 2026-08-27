namespace SeddikClinic.Core.DTOs.Settings;

public class WorkingHoursConfigDto
{
    public string StartTime { get; set; } = "17:00"; // 05:00 PM
    public string EndTime { get; set; } = "22:30";   // 10:30 PM
    public int SlotDurationMinutes { get; set; } = 30;
    public string ClinicDays { get; set; } = "يومياً عدا الجمعة";
}
