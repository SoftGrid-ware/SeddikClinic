namespace SeddikClinic.Core.Entities.Appointments;

public class ClinicService
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public decimal DefaultPrice { get; set; } = 0m;
    public string? Description { get; set; }
    public string? Category { get; set; } = "عام";
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
