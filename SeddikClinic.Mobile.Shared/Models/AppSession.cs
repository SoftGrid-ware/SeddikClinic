using SeddikClinic.Core.Enums;

namespace SeddikClinic.Mobile.Shared.Models;

public static class AppSession
{
    public static Guid? UserId { get; set; }
    public static string? UserName { get; set; }
    public static string? FullName { get; set; }
    public static UserRole? Role { get; set; }
    public static string? Token { get; set; }

    public static bool IsLoggedIn => !string.IsNullOrEmpty(UserName) && !string.IsNullOrEmpty(Token);

    public static void Clear()
    {
        UserId = null;
        UserName = null;
        FullName = null;
        Role = null;
        Token = null;
    }
}
