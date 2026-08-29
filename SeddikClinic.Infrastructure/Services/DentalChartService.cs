using Microsoft.EntityFrameworkCore;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.Entities.Appointments;
using SeddikClinic.Core.Interfaces;
using SeddikClinic.Infrastructure.Data;

namespace SeddikClinic.Infrastructure.Services;

public class DentalChartService : IDentalChartService
{
    private readonly SeddikClinicDbContext _dbContext;

    public DentalChartService(SeddikClinicDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PatientDentalChartSummaryDto> GetPatientDentalChartAsync(Guid patientId)
    {
        var patient = await _dbContext.Patients.FindAsync(patientId);
        var patientName = patient?.FullName ?? "مريض";

        var existingTeeth = await _dbContext.DentalToothRecords
            .Where(t => t.PatientId == patientId)
            .OrderBy(t => t.ToothNumber)
            .ToListAsync();

        var images = await _dbContext.PatientDentalImages
            .Where(img => img.PatientId == patientId)
            .OrderByDescending(img => img.TakenAt)
            .ToListAsync();

        var teethDtos = existingTeeth.Select(t => new DentalToothRecordDto
        {
            Id = t.Id,
            PatientId = t.PatientId,
            ToothNumber = t.ToothNumber,
            Condition = t.Condition,
            AffectedSurfaces = t.AffectedSurfaces,
            Notes = t.Notes,
            EstimatedCost = t.EstimatedCost,
            IsCompleted = t.IsCompleted,
            UpdatedAt = t.UpdatedAt
        }).ToList();

        var imageDtos = images.Select(img => new PatientDentalImageDto
        {
            Id = img.Id,
            PatientId = img.PatientId,
            Title = img.Title,
            ImageType = img.ImageType,
            ImageUrl = img.ImageUrl,
            ThumbnailUrl = img.ThumbnailUrl,
            Notes = img.Notes,
            AssociatedToothNumber = img.AssociatedToothNumber,
            TakenAt = img.TakenAt
        }).ToList();

        return new PatientDentalChartSummaryDto
        {
            PatientId = patientId,
            PatientName = patientName,
            Teeth = teethDtos,
            Images = imageDtos,
            TotalDecayed = teethDtos.Count(t => t.Condition == ToothCondition.Decayed),
            TotalFilled = teethDtos.Count(t => t.Condition == ToothCondition.Filled),
            TotalRootCanal = teethDtos.Count(t => t.Condition == ToothCondition.RootCanal),
            TotalCrowns = teethDtos.Count(t => t.Condition == ToothCondition.Crown),
            TotalMissing = teethDtos.Count(t => t.Condition == ToothCondition.Extracted),
            TotalEstimatedTreatmentCost = teethDtos.Sum(t => t.EstimatedCost)
        };
    }

    public async Task<DentalToothRecordDto> UpdateToothRecordAsync(UpdateToothRecordDto dto)
    {
        var record = await _dbContext.DentalToothRecords
            .FirstOrDefaultAsync(t => t.PatientId == dto.PatientId && t.ToothNumber == dto.ToothNumber);

        if (record == null)
        {
            record = new DentalToothRecord
            {
                PatientId = dto.PatientId,
                ToothNumber = dto.ToothNumber,
                Condition = dto.Condition,
                AffectedSurfaces = dto.AffectedSurfaces,
                Notes = dto.Notes,
                EstimatedCost = dto.EstimatedCost,
                IsCompleted = dto.IsCompleted,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.DentalToothRecords.Add(record);
        }
        else
        {
            record.Condition = dto.Condition;
            record.AffectedSurfaces = dto.AffectedSurfaces;
            record.Notes = dto.Notes;
            record.EstimatedCost = dto.EstimatedCost;
            record.IsCompleted = dto.IsCompleted;
            record.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        return new DentalToothRecordDto
        {
            Id = record.Id,
            PatientId = record.PatientId,
            ToothNumber = record.ToothNumber,
            Condition = record.Condition,
            AffectedSurfaces = record.AffectedSurfaces,
            Notes = record.Notes,
            EstimatedCost = record.EstimatedCost,
            IsCompleted = record.IsCompleted,
            UpdatedAt = record.UpdatedAt
        };
    }

    public async Task<bool> ResetPatientTeethAsync(Guid patientId)
    {
        var records = await _dbContext.DentalToothRecords.Where(t => t.PatientId == patientId).ToListAsync();
        _dbContext.DentalToothRecords.RemoveRange(records);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<PatientDentalImageDto>> GetPatientImagesAsync(Guid patientId, DentalImageType? type = null)
    {
        var query = _dbContext.PatientDentalImages.Where(img => img.PatientId == patientId);
        if (type.HasValue) query = query.Where(img => img.ImageType == type.Value);

        var list = await query.OrderByDescending(img => img.TakenAt).ToListAsync();

        return list.Select(img => new PatientDentalImageDto
        {
            Id = img.Id,
            PatientId = img.PatientId,
            Title = img.Title,
            ImageType = img.ImageType,
            ImageUrl = img.ImageUrl,
            ThumbnailUrl = img.ThumbnailUrl,
            Notes = img.Notes,
            AssociatedToothNumber = img.AssociatedToothNumber,
            TakenAt = img.TakenAt
        }).ToList();
    }

    public async Task<PatientDentalImageDto> AddPatientImageAsync(CreateDentalImageDto dto)
    {
        var img = new PatientDentalImage
        {
            PatientId = dto.PatientId,
            Title = !string.IsNullOrWhiteSpace(dto.Title) ? dto.Title : "صورة سنية",
            ImageType = dto.ImageType,
            ImageUrl = dto.ImageUrl,
            ThumbnailUrl = dto.ImageUrl,
            Notes = dto.Notes,
            AssociatedToothNumber = dto.AssociatedToothNumber,
            TakenAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.PatientDentalImages.Add(img);
        await _dbContext.SaveChangesAsync();

        return new PatientDentalImageDto
        {
            Id = img.Id,
            PatientId = img.PatientId,
            Title = img.Title,
            ImageType = img.ImageType,
            ImageUrl = img.ImageUrl,
            ThumbnailUrl = img.ThumbnailUrl,
            Notes = img.Notes,
            AssociatedToothNumber = img.AssociatedToothNumber,
            TakenAt = img.TakenAt
        };
    }

    public async Task<bool> DeletePatientImageAsync(Guid imageId)
    {
        var img = await _dbContext.PatientDentalImages.FindAsync(imageId);
        if (img == null) return false;

        _dbContext.PatientDentalImages.Remove(img);
        await _dbContext.SaveChangesAsync();
        return true;
    }
}
