using System.Web;
using SeddikClinic.Core.DTOs.Appointments;

namespace SeddikClinic.Core.Helpers;

public static class WhatsAppNotificationHelper
{
    public const string ClinicPhoneNumber = "201126092725";
    public const string ClinicName = "عيادة د. صديق لطب وجراحة وتجميل الأسنان";
    public const string ClinicFacebook = "https://www.facebook.com/SeddikDentalClinic";

    public static string FormatPhoneNumberForWhatsApp(string rawPhone)
    {
        if (string.IsNullOrWhiteSpace(rawPhone)) return "";
        var digits = new string(rawPhone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0"))
        {
            digits = "2" + digits; // Egypt country code
        }
        else if (!digits.StartsWith("20") && digits.Length == 10)
        {
            digits = "20" + digits;
        }
        return digits;
    }

    /// <summary>
    /// توليد رابط رسالة تأكيد الحجز للمريض
    /// </summary>
    public static string GenerateAppointmentConfirmationUrl(AppointmentDto appointment)
    {
        var phone = FormatPhoneNumberForWhatsApp(appointment.PatientPhone);
        var message = $@"مرحباً بك أستاذ/ة {appointment.PatientName} 🌸
تم تأكيد حجز موعدك بنجاح في *{ClinicName}* 🦷✨

📋 *تفاصيل الموعد:*
• رقم الحجز: #{appointment.AppointmentNumber}
• التاريخ: {appointment.DateFormatted}
• التوقيت: {appointment.FormattedTime}
• الخدمة الطبية: {appointment.ServiceType}
• الطبيب: {appointment.DoctorName}

📍 *العنوان:* عيادة د. صديق للأسنان
📞 *للتواصل والاستفسار:* 01126092725
🌐 *صفحتنا على فيسبوك:* {ClinicFacebook}

نتشرف بزيارتك ونتمنى لك دوام الصحة والعافية! 🌿";

        return $"https://wa.me/{phone}?text={Uri.EscapeDataString(message)}";
    }

    /// <summary>
    /// توليد رابط رسالة تذكير بالموعد قبل 24 ساعة
    /// </summary>
    public static string GenerateAppointmentReminderUrl(AppointmentDto appointment)
    {
        var phone = FormatPhoneNumberForWhatsApp(appointment.PatientPhone);
        var message = $@"تذكير بموعد الكشف في *{ClinicName}* ⏰🦷

عزيزي/تي {appointment.PatientName}،
نذكرك بموعدك القادم:
📅 التاريخ: {appointment.DateFormatted}
⏰ الوقت: {appointment.FormattedTime}
🩺 الخدمة: {appointment.ServiceType}

يرجى الحضور قبل الموعد بـ 10 دقائق. في حال الرغبة بتأجيل الموعد يرجى إبلاغنا مسبقاً.
📞 هاتف العيادة: 01126092725";

        return $"https://wa.me/{phone}?text={Uri.EscapeDataString(message)}";
    }

    /// <summary>
    /// توليد رابط تعليمات ما بعد الإجراء الجراحي أو الخلع
    /// </summary>
    public static string GeneratePostTreatmentInstructionsUrl(string patientName, string rawPhone, string treatmentType)
    {
        var phone = FormatPhoneNumberForWhatsApp(rawPhone);
        string specificInstructions;

        if (treatmentType.Contains("خلع") || treatmentType.Contains("جراحة") || treatmentType.Contains("زراعة"))
        {
            specificInstructions = @"• الضغط على الشاش بلطف لمدة 45 دقيقة وعدم البصق.
• تجنب الأطعمة والمشروبات الساخنة والتدخين لمدة 24 ساعة.
• الالتزام بتناول المسكن والمضاد الحيوي في مواعيده المحددة.
• كمادات باردة على الخد من الخارج لتخفيف الانتفاخ إن وجد.";
        }
        else if (treatmentType.Contains("تبييض") || treatmentType.Contains("تنظيف"))
        {
            specificInstructions = @"• تجنب المشروبات الملونة (الشاي، القهوة، الكولا) والتدخين لمدة 48 ساعة.
• استخدام معجون أسنان مخصص للأسنان الحساسة عند اللزوم.";
        }
        else
        {
            specificInstructions = @"• تجنب المضغ على الجانب المعالج حتى زوال مفعول البنج.
• الالتزام بالعلاج الموصوف في الروشتة الطبية.";
        }

        var message = $@"ألف سلامة عليك أستاذ/ة {patientName} 🌸
إرشادات وتعليمات هامة بعد جلسة *({treatmentType})* من *{ClinicName}* 🦷:

{specificInstructions}

📞 فريق العيادة في خدمتك دائماً لأي استفسار: 01126092725
نتمنى لك الشفاء العاجل وابتسامة مشرقة دائماً! ✨";

        return $"https://wa.me/{phone}?text={Uri.EscapeDataString(message)}";
    }

    /// <summary>
    /// توليد رابط إرسال الروشتة الطبية للمريض
    /// </summary>
    public static string GeneratePrescriptionShareUrl(PrescriptionDto prescription)
    {
        var phone = FormatPhoneNumberForWhatsApp(prescription.PatientPhone);
        var itemsText = string.Join("\n", prescription.Items.Select((item, idx) => 
            $"{idx + 1}. *{item.MedicationName}* ({item.Dosage})\n   - الجرعة: {item.Frequency}\n   - المدة: {item.Duration} {(string.IsNullOrWhiteSpace(item.Instructions) ? "" : $"- {item.Instructions}")}"));

        var message = $@"الروشتة الطبية الإلكترونية - *{ClinicName}* 💊🦷
المريض: {prescription.PatientName}
التاريخ: {prescription.FormattedDate}
الطبيب المعالج: {prescription.DoctorName}

{(string.IsNullOrWhiteSpace(prescription.Diagnosis) ? "" : $"🔍 *التشخيص:* {prescription.Diagnosis}\n")}
📋 *الأدوية الموصوفة:*
{itemsText}

{(string.IsNullOrWhiteSpace(prescription.GeneralInstructions) ? "" : $"\n💡 *تعليمات الطبيب:* {prescription.GeneralInstructions}\n")}
نتمنى لك الشفاء العاجل! 🌿";

        return $"https://wa.me/{phone}?text={Uri.EscapeDataString(message)}";
    }
}
