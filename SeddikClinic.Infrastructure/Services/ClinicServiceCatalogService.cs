using Microsoft.EntityFrameworkCore;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.Entities.Appointments;
using SeddikClinic.Core.Interfaces;
using SeddikClinic.Infrastructure.Data;

namespace SeddikClinic.Infrastructure.Services;

public class ClinicServiceCatalogService : IClinicServiceCatalogService
{
    private readonly SeddikClinicDbContext _dbContext;

    public ClinicServiceCatalogService(SeddikClinicDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ClinicServiceDto>> GetAllServicesAsync()
    {
        var services = await _dbContext.ClinicServices
            .Where(s => s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Name)
            .ToListAsync();

        // لو كان الجدول فارغاً نقوم بتهيئته فوراً
        if (!services.Any())
        {
            await InitializeDefaultServicesAsync();
            services = await _dbContext.ClinicServices
                .Where(s => s.IsActive)
                .OrderBy(s => s.DisplayOrder)
                .ThenBy(s => s.Name)
                .ToListAsync();
        }

        return services.Select(MapToDto).ToList();
    }

    public async Task<ClinicServiceDto?> GetServiceByIdAsync(Guid id)
    {
        var service = await _dbContext.ClinicServices.FindAsync(id);
        if (service == null || !service.IsActive) return null;
        return MapToDto(service);
    }

    public async Task<ClinicServiceDto> CreateServiceAsync(CreateClinicServiceDto dto)
    {
        var nextOrder = await _dbContext.ClinicServices.CountAsync() + 1;
        var service = new ClinicService
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            DefaultPrice = dto.DefaultPrice,
            Description = dto.Description,
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "عام" : dto.Category,
            DisplayOrder = dto.DisplayOrder > 0 ? dto.DisplayOrder : nextOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ClinicServices.Add(service);
        await _dbContext.SaveChangesAsync();

        return MapToDto(service);
    }

    public async Task<ClinicServiceDto?> UpdateServiceAsync(Guid id, UpdateClinicServiceDto dto)
    {
        var service = await _dbContext.ClinicServices.FindAsync(id);
        if (service == null) return null;

        service.Name = dto.Name.Trim();
        service.DefaultPrice = dto.DefaultPrice;
        service.Description = dto.Description;
        if (!string.IsNullOrWhiteSpace(dto.Category)) service.Category = dto.Category;
        service.IsActive = dto.IsActive;
        if (dto.DisplayOrder > 0) service.DisplayOrder = dto.DisplayOrder;

        await _dbContext.SaveChangesAsync();
        return MapToDto(service);
    }

    public async Task<bool> DeleteServiceAsync(Guid id)
    {
        var service = await _dbContext.ClinicServices.FindAsync(id);
        if (service == null) return false;

        service.IsActive = false; // حذف منطقي
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateConsultationPriceAsync(decimal newPrice)
    {
        var consultationService = await _dbContext.ClinicServices
            .FirstOrDefaultAsync(s => s.Name.Contains("كشف") && s.IsActive);

        if (consultationService != null)
        {
            consultationService.DefaultPrice = newPrice;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        // إذا لم يكن موجوداً ننشئه
        await CreateServiceAsync(new CreateClinicServiceDto
        {
            Name = "كشف واستشارة طبية",
            DefaultPrice = newPrice,
            Category = "كشف وفحص",
            DisplayOrder = 1
        });

        return true;
    }

    private async Task InitializeDefaultServicesAsync()
    {
        var defaults = new List<ClinicService>
        {
            new() { Id = Guid.NewGuid(), Name = "كشف واستشارة طبية", DefaultPrice = 250m, Category = "كشف وفحص", DisplayOrder = 1, IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "حشو أسنان كمبوزيت", DefaultPrice = 500m, Category = "علاج وتجميل", DisplayOrder = 2, IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "علاج جذور وعصب", DefaultPrice = 800m, Category = "علاج وتجميل", DisplayOrder = 3, IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "تنظيف وتلميع أسنان وتكلسات", DefaultPrice = 400m, Category = "وقاية وتجميل", DisplayOrder = 4, IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "تبييض أسنان احترافي", DefaultPrice = 1500m, Category = "وقاية وتجميل", DisplayOrder = 5, IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "تركيبات وتيجان زيركون", DefaultPrice = 2500m, Category = "تركيبات", DisplayOrder = 6, IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "زراعة أسنان", DefaultPrice = 5000m, Category = "جراحة وزراعة", DisplayOrder = 7, IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "تقويم أسنان", DefaultPrice = 10000m, Category = "تقويم", DisplayOrder = 8, IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "خلع ضرس وجراحة", DefaultPrice = 350m, Category = "جراحة وزراعة", DisplayOrder = 9, IsActive = true }
        };

        _dbContext.ClinicServices.AddRange(defaults);
        await _dbContext.SaveChangesAsync();
    }

    private static ClinicServiceDto MapToDto(ClinicService s)
    {
        return new ClinicServiceDto
        {
            Id = s.Id,
            Name = s.Name,
            DefaultPrice = s.DefaultPrice,
            Description = s.Description,
            Category = s.Category,
            IsActive = s.IsActive,
            DisplayOrder = s.DisplayOrder
        };
    }
}
