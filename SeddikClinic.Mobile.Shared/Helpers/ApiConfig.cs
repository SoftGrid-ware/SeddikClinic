namespace SeddikClinic.Mobile.Shared.Helpers;

public static class ApiConfig
{
    // الرابط الأونلاين المباشر لسيرفر الويندوز المشترك (يعمل من أي مكان عبر الإنترنت وباقة الموبايل)
    public const string DefaultOnlineUrl = "https://broken-maker-dimensional-captain.trycloudflare.com";
    public const string DefaultLanUrl = "http://192.168.1.12:5000";
    public const string DefaultEmulatorUrl = "http://10.0.2.2:5000";

    private static string _baseUrl = DefaultOnlineUrl;

    public static string BaseUrl
    {
        get => _baseUrl;
        set => _baseUrl = string.IsNullOrWhiteSpace(value) ? DefaultOnlineUrl : value.TrimEnd('/');
    }

    public static string[] FallbackUrls { get; } = new[]
    {
        DefaultOnlineUrl,
        "https://reviewed-alloy-href-ban.trycloudflare.com",
        DefaultLanUrl,
        "http://192.168.1.12:8080",
        DefaultEmulatorUrl,
        "http://10.0.2.2:8080",
        "http://localhost:5000"
    };
}
