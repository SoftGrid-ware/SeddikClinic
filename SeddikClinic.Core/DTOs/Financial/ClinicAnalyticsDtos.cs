namespace SeddikClinic.Core.DTOs.Financial;

public class ClinicAnalyticsOverviewDto
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit { get; set; }
    public int TotalPatientsCount { get; set; }
    public int NewPatientsThisMonth { get; set; }
    public int TotalAppointmentsCount { get; set; }
    public int CompletedAppointmentsCount { get; set; }
    public int CancelledAppointmentsCount { get; set; }
    public double AttendanceRatePercentage { get; set; } // نسبة الحضور
    public double NoShowRatePercentage { get; set; } // نسبة الغياب

    public List<TopServicePerformanceDto> TopServices { get; set; } = new();
    public List<MonthlyFinancialTrendDto> MonthlyTrends { get; set; } = new();
    public List<PeakHoursDistributionDto> PeakHours { get; set; } = new();
    public AiPracticeRecommendationDto? AiInsights { get; set; }
}

public class TopServicePerformanceDto
{
    public string ServiceName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int BookingsCount { get; set; }
    public decimal TotalRevenueGenerated { get; set; }
    public double PercentageOfTotalRevenue { get; set; }
}

public class MonthlyFinancialTrendDto
{
    public string MonthName { get; set; } = string.Empty; // e.g. "يناير 2026"
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetProfit { get; set; }
    public int PatientsCount { get; set; }
}

public class PeakHoursDistributionDto
{
    public string TimeSlot { get; set; } = string.Empty; // e.g. "05:00 م - 06:00 م"
    public int AppointmentsCount { get; set; }
    public double Percentage { get; set; }
}

public class AiPracticeRecommendationDto
{
    public string ClinicalSummary { get; set; } = string.Empty;
    public List<string> ActionableRecommendations { get; set; } = new();
    public string BestPerformingCategory { get; set; } = string.Empty;
    public string SuggestedGrowthArea { get; set; } = string.Empty;
}

public class PatientAiDiagnosisRequestDto
{
    public Guid PatientId { get; set; }
    public string ChiefComplaint { get; set; } = string.Empty; // الشكوى الرئيسية
    public string? MedicalHistoryNotes { get; set; }
    public List<int>? InvolvedTeeth { get; set; }
}

public class PatientAiDiagnosisResultDto
{
    public string Summary { get; set; } = string.Empty;
    public List<string> DifferentialDiagnoses { get; set; } = new();
    public List<string> RecommendedClinicalProcedures { get; set; } = new();
    public List<string> SuggestedMedications { get; set; } = new();
    public List<string> CautionsAndAllergiesAlerts { get; set; } = new();
}
