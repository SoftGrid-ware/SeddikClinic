namespace SeddikClinic.Core.DTOs.Appointments;

public class ClinicServiceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal DefaultPrice { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }

    public bool ShowPrice => Name.Contains("كشف") || Name.Contains("استشارة") || Name.Contains("فحص") || Category == "كشوفات";
    public string DisplayPriceText => ShowPrice ? $"{DefaultPrice:N0} ج.م" : "يحدد بعد الفحص";
}

public class CreateClinicServiceDto
{
    public string Name { get; set; } = string.Empty;
    public decimal DefaultPrice { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; } = "عام";
    public int DisplayOrder { get; set; } = 0;
}

public class UpdateClinicServiceDto
{
    public string Name { get; set; } = string.Empty;
    public decimal DefaultPrice { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public class SelectedServiceItemDto
{
    public Guid? ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
