using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SeddikClinic.Desktop.Services;

namespace SeddikClinic.Desktop.Views;

public partial class FacebookMarketingView : UserControl
{
    private readonly ClinicApiClient _apiClient;

    public FacebookMarketingView(ClinicApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        GenerateAiPost();
    }

    private void PostTopicCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            GenerateAiPost();
        }
    }

    private void GenerateAiPost_Click(object sender, RoutedEventArgs e)
    {
        GenerateAiPost();
    }

    private void RegeneratePost_Click(object sender, RoutedEventArgs e)
    {
        GenerateAiPost();
    }

    private void GenerateAiPost()
    {
        var selectedTopic = (PostTopicCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "🦷 زراعة الأسنان";
        var phone = ClinicPhoneInput?.Text?.Trim() ?? "01009563353";
        var address = ClinicAddressInput?.Text?.Trim() ?? "عيادة د. صديق لطب الأسنان";
        var offer = OfferDiscountInput?.Text?.Trim() ?? "";

        string postTitle;
        string postBody;
        string hashtags;

        if (selectedTopic.Contains("زراعة"))
        {
            postTitle = "🦷 رجّع ابتسامتك وثقتك بنفسك مع زراعة الأسنان الفورية وبدون ألم! ✨";
            postBody = $"هل تعاني من فقدان سن أو أكثر ومتردد من الألم؟\nفي عيادة د. صديق بنقدملك أحدث تقنيات زراعة الأسنان بدون ألم وبنسبة نجاح تفوق 98% باستخدام أجود أنواع الغرسات الألمانية والكورية المعتمدة دولياً 🇩🇪🇰🇷.\n\n" +
                       $"⭐ مميزات الزراعة معنا:\n" +
                       $"• زراعة فورية في نفس الجلسة بدون جراحة معقدة.\n" +
                       $"• ثبات عالي ومظهر طبيعي يماثل الأسنان الأصلية تماماً.\n" +
                       $"• خطط سداد ميسرة وإمكانية التقسيط المريح تناسب جميع الحالات.\n\n" +
                       (!string.IsNullOrWhiteSpace(offer) ? $"🎁 {offer}\n\n" : "");
            hashtags = "#زراعة_الأسنان #عيادة_د_صديق #طبيب_أسنان #تجميل_الأسنان #DentalImplants #ابتسامة_جديدة";
        }
        else if (selectedTopic.Contains("تبييض") || selectedTopic.Contains("هوليوود"))
        {
            postTitle = "✨ ابتسامة ناصعة البياض تليق بجمالك في 45 دقيقة فقط! 💎";
            postBody = $"احصل على أسنان بيضاء لامعة بأحدث أجهزة تبييض الأسنان بالليزر وتقنية Zoom الأمريكية 🇺🇸.\n" +
                       $"تخلص نهائياً من تصبغات الشاي والقهوة والتدخين بطريقة آمنة تماماً على طبقة المينا ودون حساسية.\n\n" +
                       $"🌟 كشف مجاني واستشارة تجميلية فورية مع كل جلسة تبييض!\n\n" +
                       (!string.IsNullOrWhiteSpace(offer) ? $"🎉 {offer}\n\n" : "");
            hashtags = "#تبييض_الأسنان #ابتسامة_هوليوود #تجميل_الأسنان #HollywoodSmile #LaserWhitening #عيادة_د_صديق";
        }
        else if (selectedTopic.Contains("تقويم"))
        {
            postTitle = "😁 رتّب أسنانك واكتسب ابتسامة متناسقة وجذابة مع أحدث أنواع التقويم! 🌟";
            postBody = $"مشاكل تزاحم الأسنان أو العضة غير المنتظمة أصبح حلها أسهل مما تتخيل.\n" +
                       $"نقدم في عيادة د. صديق:\n" +
                       $"• التقويم الشفاف غير المرئي (Clear Aligners).\n" +
                       $"• التقويم المعدني والخزفي الدقيق للأطفال والشباب.\n" +
                       $"• خطط تقسيط شهرية مريحة جداً تناسب ميزانيتك.\n\n" +
                       (!string.IsNullOrWhiteSpace(offer) ? $"🎁 {offer}\n\n" : "");
            hashtags = "#تقويم_الأسنان #التقويم_الشفاف #ابتسامة_مثالية #Orthodontics #عيادة_د_صديق";
        }
        else if (selectedTopic.Contains("جذور") || selectedTopic.Contains("أعصاب"))
        {
            postTitle = "🩺 ودّع ألم الأسنان الحاد في جلسة واحدة مع علاج الجذور الميكروسكوبي! ⚡";
            postBody = $"ألم العصب لا يحتمل؟ علاج الجذور بالتقنيات الحديثة (Endo Rotary) أصبح يتم في جلسة واحدة وبدون أي شعور بالألم.\n" +
                       $"نقوم بتنظيف وحشو قنوات العصب بأعلى دقة لإنقاذ السن وحمايته من الخلع.\n\n" +
                       (!string.IsNullOrWhiteSpace(offer) ? $"💡 {offer}\n\n" : "");
            hashtags = "#علاج_عصب #علاج_جذور #طب_أسنان #بدون_ألم #Endodontics #عيادة_د_صديق";
        }
        else if (selectedTopic.Contains("الأطفال"))
        {
            postTitle = "👶 ابتسامة طفلك أمانة.. كشف أسنان ممتع وخالي من الخوف! 🎈";
            postBody = $"نهتم بصحة أسنان أطفالكم في بيئة مرحة وودودة تزيل أي خوف من طبيب الأسنان.\n" +
                       $"• علاج تسوس الأسنان اللبنية وجلسات الفلورايد لحماية الأسنان.\n" +
                       $"• حافظات المسافة لضمان نمو الأسنان الدائمة بشكل سليم.\n\n" +
                       (!string.IsNullOrWhiteSpace(offer) ? $"✨ {offer}\n\n" : "");
            hashtags = "#أسنان_الأطفال #صحة_الطفل #طب_أسنان #PediatricDentistry #عيادة_د_صديق";
        }
        else if (selectedTopic.Contains("الزيركون") || selectedTopic.Contains("Veneers"))
        {
            postTitle = "💎 استعد قوة وجمال أسنانك مع تركيبات الزيركون والعدسات التجميلية! 👑";
            postBody = $"تركيبات الزيركون الألمانية والـ E-max تمنحك قوة فائقة وشفافية طبيعية 100% بدون أي خطوط داكنة عند اللثة.\n" +
                       $"• ضمان شامل على جودة التركيبات والصلابة.\n" +
                       $"• تصميم رقمي دقيق ببرامج الـ CAD/CAM لضمان الراحة التامة.\n\n" +
                       (!string.IsNullOrWhiteSpace(offer) ? $"🎉 {offer}\n\n" : "");
            hashtags = "#زيركون #عدسات_الأسنان #فينير #Zirconia #Veneers #عيادة_د_صديق";
        }
        else
        {
            postTitle = "🎉 فرصة مميزة للعناية بأسنانك وصحة ابتسامتك! 🦷✨";
            postBody = $"صحة فمك وأسنانك هي مرآة صحتك العامة. لا تؤجل الفحص الدوري وتنظيف الجير لحماية لثتك وأسنانك من التسوس والالتهاب.\n\n" +
                       $"فريقنا الطبي في انتظارك بأحدث أجهزة التعقيم والراحة التامة.\n\n" +
                       (!string.IsNullOrWhiteSpace(offer) ? $"🎁 {offer}\n\n" : "");
            hashtags = "#عروض_الأسنان #صحة_الفم #طبيب_أسنان #عيادة_د_صديق #DentalClinic";
        }

        var fullPost = $"{postTitle}\n\n" +
                       $"{postBody}" +
                       $"━━━━━━━━━━━━━━━━━━━━━\n" +
                       $"📍 العنوان: {address}\n" +
                       $"📞 للحجز والاستفسار المباشر: {phone}\n" +
                       $"💬 أو راسلنا عبر الواتساب مباشرة: https://wa.me/2{phone}\n" +
                       $"━━━━━━━━━━━━━━━━━━━━━\n\n" +
                       $"{hashtags}";

        if (PostContentEditor != null) PostContentEditor.Text = fullPost;
        if (LivePreviewBodyText != null) LivePreviewBodyText.Text = fullPost;
    }

    private void CopyPostText_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(PostContentEditor?.Text))
        {
            Clipboard.SetText(PostContentEditor.Text);
            ClinicMessageBox.Show("تم نسخ نص المنشور بنجاح إلى الحافظة! جاهز للصق على فيسبوك.", "تم النسخ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void PublishToFacebook_Click(object sender, RoutedEventArgs e)
    {
        CopyPostText_Click(sender, e);
        try
        {
            Process.Start(new ProcessStartInfo("https://facebook.com") { UseShellExecute = true });
        }
        catch { }
    }

    private void OpenFacebookPage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://facebook.com") { UseShellExecute = true });
        }
        catch { }
    }

    private void OpenMetaAdsManager_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://adsmanager.facebook.com") { UseShellExecute = true });
        }
        catch { }
    }
}
