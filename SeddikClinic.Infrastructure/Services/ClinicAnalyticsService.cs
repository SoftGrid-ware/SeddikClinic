using Microsoft.EntityFrameworkCore;
using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Core.Enums;
using SeddikClinic.Core.Interfaces;
using SeddikClinic.Infrastructure.Data;

namespace SeddikClinic.Infrastructure.Services;

public class ClinicAnalyticsService : IClinicAnalyticsService
{
    private readonly SeddikClinicDbContext _dbContext;

    public ClinicAnalyticsService(SeddikClinicDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ClinicAnalyticsOverviewDto> GetClinicAnalyticsOverviewAsync(int monthsBack = 6)
    {
        var startDate = DateTime.UtcNow.AddMonths(-monthsBack);
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var appointments = await _dbContext.Appointments
            .Where(a => !a.IsDeleted && a.AppointmentDate >= startDate)
            .ToListAsync();

        var expenses = await _dbContext.Expenses
            .Where(e => !e.IsDeleted && e.PaymentDate >= startDate)
            .ToListAsync();

        var totalPatients = await _dbContext.Patients.CountAsync(p => !p.IsDeleted);
        var newPatientsThisMonth = await _dbContext.Patients.CountAsync(p => !p.IsDeleted && p.CreatedAt >= startOfMonth);

        var completedAppointments = appointments.Where(a => a.Status == AppointmentStatus.Completed).ToList();
        var cancelledAppointments = appointments.Where(a => a.Status == AppointmentStatus.Cancelled).ToList();
        var totalAppointments = appointments.Count;

        var totalRevenue = appointments
            .Where(a => a.Status != AppointmentStatus.Cancelled && a.DepositAmount > 0)
            .Sum(a => Math.Min(a.DepositAmount, Math.Max(0, a.TotalFees - a.DiscountAmount)));
        var totalExpensesAmount = expenses.Sum(e => e.Amount);
        var netProfit = totalRevenue - totalExpensesAmount;

        double attendanceRate = totalAppointments > 0 ? Math.Round((double)completedAppointments.Count / totalAppointments * 100, 1) : 100.0;
        double noShowRate = totalAppointments > 0 ? Math.Round((double)cancelledAppointments.Count / totalAppointments * 100, 1) : 0.0;

        // تحليل أداء الخدمات الأكثر طلباً وربحية
        var serviceGroups = appointments
            .Where(a => a.Status != AppointmentStatus.Cancelled && !string.IsNullOrWhiteSpace(a.ServiceType))
            .GroupBy(a => a.ServiceType!.Trim())
            .Select(g => new TopServicePerformanceDto
            {
                ServiceName = g.Key,
                Category = "خدمات العيادة",
                BookingsCount = g.Count(),
                TotalRevenueGenerated = g.Where(a => a.DepositAmount > 0).Sum(a => Math.Min(a.DepositAmount, Math.Max(0, a.TotalFees - a.DiscountAmount))),
                PercentageOfTotalRevenue = totalRevenue > 0 ? Math.Round((double)g.Where(a => a.DepositAmount > 0).Sum(a => Math.Min(a.DepositAmount, Math.Max(0, a.TotalFees - a.DiscountAmount))) / (double)totalRevenue * 100, 1) : 0
            })
            .OrderByDescending(s => s.TotalRevenueGenerated)
            .Take(6)
            .ToList();

        // التوزيع الشهري
        var monthlyTrends = new List<MonthlyFinancialTrendDto>();
        for (int i = monthsBack - 1; i >= 0; i--)
        {
            var mDate = DateTime.UtcNow.AddMonths(-i);
            var mStart = new DateTime(mDate.Year, mDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var mEnd = mStart.AddMonths(1);

            var mPeriodApts = appointments.Where(a => a.AppointmentDate >= mStart && a.AppointmentDate < mEnd && a.Status != AppointmentStatus.Cancelled && a.DepositAmount > 0).ToList();
            var mRev = mPeriodApts.Sum(a => Math.Min(a.DepositAmount, Math.Max(0, a.TotalFees - a.DiscountAmount)));
            var mExp = expenses.Where(e => e.PaymentDate >= mStart && e.PaymentDate < mEnd).Sum(e => e.Amount);

            var arCulture = new System.Globalization.CultureInfo("ar-EG");
            monthlyTrends.Add(new MonthlyFinancialTrendDto
            {
                MonthName = mStart.ToString("MMMM yyyy", arCulture),
                Revenue = mRev,
                Expenses = mExp,
                NetProfit = mRev - mExp,
                PatientsCount = mPeriodApts.Count
            });
        }

        // تحليل أوقات الذروة
        var peakHours = appointments
            .GroupBy(a => a.StartTime.Hours)
            .Select(g =>
            {
                var hour = g.Key;
                var displayHour = hour > 12 ? $"{hour - 12}:00 م" : (hour == 12 ? "12:00 م" : $"{hour}:00 ص");
                return new PeakHoursDistributionDto
                {
                    TimeSlot = displayHour,
                    AppointmentsCount = g.Count(),
                    Percentage = totalAppointments > 0 ? Math.Round((double)g.Count() / totalAppointments * 100, 1) : 0
                };
            })
            .OrderByDescending(p => p.AppointmentsCount)
            .Take(5)
            .ToList();

        // توليد توصيات ذكية تلقائية للعيادة
        var topService = serviceGroups.FirstOrDefault()?.ServiceName ?? "كشف واستشارة";
        var aiRecommendations = new AiPracticeRecommendationDto
        {
            ClinicalSummary = $"العيادة تحقق معدل حضور متميز ({attendanceRate}%)، وإجمالي إيرادات ({totalRevenue:N0} ج.م) مع تدفق مستمر للمرضى.",
            BestPerformingCategory = topService,
            SuggestedGrowthArea = "زيادة باقات تجميل وتبييض الأسنان وتركيبات الزيركون وتفعيل التذكير بالواتساب لتقليل الإلغاءات.",
            ActionableRecommendations = new List<string>
            {
                $"الخدمة الأعلى دخلاً في العيادة هي ({topService}) بنسبة مساهمة {serviceGroups.FirstOrDefault()?.PercentageOfTotalRevenue}% من الإيرادات.",
                attendanceRate >= 85 ? "معدل التزام المرضى ممتاز جداً يفوق 85%." : "يوصى بتفعيل رسائل التذكير التلقائية عبر الواتساب قبل 24 ساعة لرفع نسبة الحضور.",
                "الحفاظ على تسجيل المصروفات الدورية أولاً بأول يضمن قياس صافي أرباح العيادة بدقة عالية."
            }
        };

        return new ClinicAnalyticsOverviewDto
        {
            TotalRevenue = totalRevenue,
            TotalExpenses = totalExpensesAmount,
            NetProfit = netProfit,
            TotalPatientsCount = totalPatients,
            NewPatientsThisMonth = newPatientsThisMonth,
            TotalAppointmentsCount = totalAppointments,
            CompletedAppointmentsCount = completedAppointments.Count,
            CancelledAppointmentsCount = cancelledAppointments.Count,
            AttendanceRatePercentage = attendanceRate,
            NoShowRatePercentage = noShowRate,
            TopServices = serviceGroups,
            MonthlyTrends = monthlyTrends,
            PeakHours = peakHours,
            AiInsights = aiRecommendations
        };
    }

    public async Task<PatientAiDiagnosisResultDto> GetAiDiagnosticRecommendationsAsync(PatientAiDiagnosisRequestDto request)
    {
        var patient = await _dbContext.Patients.FindAsync(request.PatientId);
        var complaint = request.ChiefComplaint?.ToLower() ?? "";
        var medHistory = (patient?.MedicalHistory ?? "") + " " + (request.MedicalHistoryNotes ?? "");

        var diffDiagnoses = new List<string>();
        var procedures = new List<string>();
        var meds = new List<string>();
        var alerts = new List<string>();

        if (complaint.Contains("وجع") || complaint.Contains("ألم") || complaint.Contains("عصب") || complaint.Contains("نبض") || complaint.Contains("سخونة"))
        {
            diffDiagnoses.Add("التهاب عصب حاد غير رجعي (Irreversible Pulpitis)");
            diffDiagnoses.Add("خراج ذروي حاد (Acute Periapical Abscess)");
            procedures.Add("فتح حجرة العصب واستئصال اللب الحيوي (Pulpectomy)");
            procedures.Add("تنظيف وتشكيل قنوات الجذور (Root Canal Instrumentation)");
            meds.Add("Augmentin 1g (أوجمنتين) - قرص كل 12 ساعة");
            meds.Add("Cataflam 50mg (كاتافلام) - قرص كل 8 ساعات بعد الأكل");
        }
        else if (complaint.Contains("نزيف") || complaint.Contains("لثة") || complaint.Contains("رائحة") || complaint.Contains("جير"))
        {
            diffDiagnoses.Add("التهاب لثة مزمن وتراكم تكلسات (Chronic Marginal Gingivitis)");
            diffDiagnoses.Add("التهاب دواعم السن (Periodontitis)");
            procedures.Add("جلسة إزالة الجير والتكلسات بالموجات فوق الصوتية (Ultrasonic Scaling)");
            procedures.Add("تلميع الأسنان وتطبيق الفلورايد (Polishing & Fluoride)");
            meds.Add("Hexitol Mouthwash (مضمضة هكستول 0.12%) - مرتين يومياً");
            meds.Add("Gengigel Oral Gel (جل جنجيجيل للثة) - 3 مرات يومياً");
        }
        else if (complaint.Contains("تجميل") || complaint.Contains("اصفرار") || complaint.Contains("شكل") || complaint.Contains("فرق"))
        {
            diffDiagnoses.Add("تصبغات سنية وتغير لون المينا (Extrinsic/Intrinsic Staining)");
            diffDiagnoses.Add("سوء إطباق خفيف أو فراغات سنية (Mild Diastema)");
            procedures.Add("جلسة تبييض ليزر احترافي (In-Office Laser Teeth Whitening)");
            procedures.Add("تركيبات وعدسات فينير زيركون (Porcelain / E-max Veneers)");
        }
        else
        {
            diffDiagnoses.Add("فحص وتقييم شامل للأسنان واللثة (Comprehensive Dental Examination)");
            procedures.Add("عمل أشعة تشخيصية (Periapical / Bitewing X-Ray)");
            procedures.Add("وضع خطة علاجية مخصصة للسن المتأثر");
        }

        // فحص الحساسية والأمراض المزمنة
        if (medHistory.Contains("بنسلين") || medHistory.Contains("penicillin") || medHistory.Contains("حساسية"))
        {
            alerts.Add("⚠️ تنبيه هام: المريض يعاني من حساسية البنسلين ومشتقاته (يمنع وصف Augmentin / Amoxicillin). البديل: Clindamycin 300mg أو Erythromycin.");
            meds.RemoveAll(m => m.Contains("Augmentin"));
            meds.Add("Dalacin C (Clindamycin) 300mg - كبسولة كل 8 ساعات (بديل آمن لحساسية البنسلين)");
        }

        if (medHistory.Contains("سكر") || medHistory.Contains("diabetes"))
        {
            alerts.Add("⚠️ تنبيه: مريض سكري - يرجى التأكد من ضبط معدل السكر التراكمي ومتابعة التئام الجروح واللثة بحرص.");
        }

        if (medHistory.Contains("ضغط") || medHistory.Contains("سيولة") || medHistory.Contains("قلب") || medHistory.Contains("aspirin"))
        {
            alerts.Add("⚠️ تنبيه: مريض ضغط/قلب - استخدام بنج خالي من الأدرينالين (Mepivacaine 3% without vasoconstrictor) في حالات الضغط المرتفع.");
        }

        return new PatientAiDiagnosisResultDto
        {
            Summary = $"بناءً على الشكوى السريرية ({complaint}) والتاريخ الصحي للمريض:",
            DifferentialDiagnoses = diffDiagnoses,
            RecommendedClinicalProcedures = procedures,
            SuggestedMedications = meds,
            CautionsAndAllergiesAlerts = alerts
        };
    }
}
