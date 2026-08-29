using Microsoft.EntityFrameworkCore;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.Entities.Billing;
using SeddikClinic.Core.Interfaces;
using SeddikClinic.Infrastructure.Data;

namespace SeddikClinic.Infrastructure.Services;

public class PrescriptionService : IPrescriptionService
{
    private readonly SeddikClinicDbContext _dbContext;

    public PrescriptionService(SeddikClinicDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<PrescriptionDto>> GetPatientPrescriptionsAsync(Guid patientId)
    {
        var list = await _dbContext.Prescriptions
            .Include(p => p.Patient)
            .Include(p => p.Items)
            .Where(p => p.PatientId == patientId && !p.IsDeleted)
            .OrderByDescending(p => p.IssuedAt)
            .ToListAsync();

        return list.Select(MapToDto).ToList();
    }

    public async Task<PrescriptionDto?> GetPrescriptionByIdAsync(Guid prescriptionId)
    {
        var p = await _dbContext.Prescriptions
            .Include(p => p.Patient)
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == prescriptionId && !p.IsDeleted);

        return p != null ? MapToDto(p) : null;
    }

    public async Task<PrescriptionDto> CreatePrescriptionAsync(CreatePrescriptionDto dto)
    {
        var patient = await _dbContext.Patients.FindAsync(dto.PatientId);
        var countToday = await _dbContext.Prescriptions.CountAsync(p => p.IssuedAt.Date == DateTime.UtcNow.Date);
        var rxNumber = $"RX-{DateTime.UtcNow:yyyyMMdd}-{(countToday + 1):D3}";

        var prescription = new Prescription
        {
            PrescriptionNumber = rxNumber,
            PatientId = dto.PatientId,
            AppointmentId = dto.AppointmentId,
            DoctorName = !string.IsNullOrWhiteSpace(dto.DoctorName) ? dto.DoctorName : "د. صديق",
            Diagnosis = dto.Diagnosis,
            GeneralInstructions = dto.GeneralInstructions,
            IssuedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        if (dto.Items != null && dto.Items.Any())
        {
            int order = 1;
            foreach (var item in dto.Items)
            {
                prescription.Items.Add(new PrescriptionItem
                {
                    MedicationName = item.MedicationName,
                    Dosage = item.Dosage,
                    Frequency = item.Frequency,
                    Duration = item.Duration,
                    Instructions = item.Instructions,
                    DisplayOrder = order++
                });
            }
        }

        _dbContext.Prescriptions.Add(prescription);
        await _dbContext.SaveChangesAsync();

        var created = await _dbContext.Prescriptions
            .Include(p => p.Patient)
            .Include(p => p.Items)
            .FirstAsync(p => p.Id == prescription.Id);

        return MapToDto(created);
    }

    public async Task<bool> DeletePrescriptionAsync(Guid prescriptionId)
    {
        var p = await _dbContext.Prescriptions.FindAsync(prescriptionId);
        if (p == null) return false;

        p.IsDeleted = true;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<DentalDrugCatalogItemDto>> GetCommonDentalDrugsCatalogAsync()
    {
        // إذا كان الكتالوج فارغاً، نقوم ببذره بالأدوية الأساسية لطب الأسنان
        if (!await _dbContext.DentalDrugCatalogItems.AnyAsync())
        {
            var seedDrugs = new List<DentalDrugCatalogItem>
            {
                new() { TradeName = "Augmentin", ScientificName = "Amoxicillin + Clavulanic Acid", Category = "مضاد حيوي", DefaultDosage = "1g", DefaultFrequency = "قرص كل 12 ساعة بعد الأكل", DefaultDuration = "6 أيام", DefaultInstructions = "إكمال كورس العلاج كاملاً" },
                new() { TradeName = "Cataflam", ScientificName = "Diclofenac Potassium", Category = "مسكن ومضاد للالتهاب", DefaultDosage = "50mg", DefaultFrequency = "قرص كل 8 ساعات عند اللزوم بعد الأكل", DefaultDuration = "3 إلى 5 أيام", DefaultInstructions = "تجنب تناوله على معدة فارغة" },
                new() { TradeName = "Panadol Extra", ScientificName = "Paracetamol + Caffeine", Category = "مسكن وخافض حرارة", DefaultDosage = "500mg", DefaultFrequency = "قرصين كل 8 ساعات عند اللزوم", DefaultDuration = "3 أيام", DefaultInstructions = "آمن مع أغلب الحالات" },
                new() { TradeName = "Flagyl", ScientificName = "Metronidazole", Category = "مضاد حيوي ولا هوائي", DefaultDosage = "500mg", DefaultFrequency = "قرص كل 8 ساعات بعد الأكل", DefaultDuration = "5 أيام", DefaultInstructions = "يمنع شرب الكحول أو المنبهات بكثرة أثناء العلاج" },
                new() { TradeName = "Alphintern", ScientificName = "Chymotrypsin + Trypsin", Category = "مضاد للورم والالتهاب", DefaultDosage = "قرص", DefaultFrequency = "قرص 3 مرات يومياً قبل الأكل بساعة", DefaultDuration = "5 أيام", DefaultInstructions = "يؤخذ قبل الأكل بساعة كاملة لامتصاص فعال" },
                new() { TradeName = "Hexitol Mouthwash", ScientificName = "Chlorhexidine Gluconate 0.12%", Category = "غسول فم ومطهر", DefaultDosage = "15ml", DefaultFrequency = "مضمضة مرتين يومياً لمدة دقيقة", DefaultDuration = "7 أيام", DefaultInstructions = "عدم الأكل أو الشرب لمدة 30 دقيقة بعد المضمضة" },
                new() { TradeName = "Gengigel Oral Gel", ScientificName = "Hyaluronic Acid", Category = "جل ملطف للثة والجروح", DefaultDosage = "طبقة رقيقة", DefaultFrequency = "دهان موضعي للثة 3 مرات يومياً", DefaultDuration = "5 أيام", DefaultInstructions = "بعد تنظيف الأسنان بالفرشاة" }
            };

            _dbContext.DentalDrugCatalogItems.AddRange(seedDrugs);
            await _dbContext.SaveChangesAsync();
        }

        var list = await _dbContext.DentalDrugCatalogItems.OrderBy(d => d.Category).ThenBy(d => d.TradeName).ToListAsync();

        return list.Select(d => new DentalDrugCatalogItemDto
        {
            Id = d.Id,
            TradeName = d.TradeName,
            ScientificName = d.ScientificName,
            Category = d.Category,
            DefaultDosage = d.DefaultDosage,
            DefaultFrequency = d.DefaultFrequency,
            DefaultDuration = d.DefaultDuration,
            DefaultInstructions = d.DefaultInstructions
        }).ToList();
    }

    private static PrescriptionDto MapToDto(Prescription p)
    {
        return new PrescriptionDto
        {
            Id = p.Id,
            PrescriptionNumber = p.PrescriptionNumber,
            PatientId = p.PatientId,
            PatientName = p.Patient?.FullName ?? "مريض",
            PatientPhone = p.Patient?.PhoneNumber ?? "",
            PatientAge = p.Patient?.Age,
            AppointmentId = p.AppointmentId,
            DoctorName = p.DoctorName,
            Diagnosis = p.Diagnosis,
            GeneralInstructions = p.GeneralInstructions,
            IssuedAt = p.IssuedAt,
            Items = p.Items.OrderBy(i => i.DisplayOrder).Select(i => new PrescriptionItemDto
            {
                Id = i.Id,
                MedicationName = i.MedicationName,
                Dosage = i.Dosage,
                Frequency = i.Frequency,
                Duration = i.Duration,
                Instructions = i.Instructions,
                DisplayOrder = i.DisplayOrder
            }).ToList()
        };
    }
}
