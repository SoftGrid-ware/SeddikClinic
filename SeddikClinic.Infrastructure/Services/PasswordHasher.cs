using System.Security.Cryptography;
using System.Text;

namespace SeddikClinic.Infrastructure.Services;

public static class PasswordHasher
{
    public static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return "";
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password + "SEDDIC_CLINIC_SALT_2026");
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(password)) return false;

        // 1. تطابق كلمة المرور الصريحة (Plaintext fallback)
        if (storedHash.Equals(password, StringComparison.Ordinal)) return true;

        // 2. تطابق الـ Hash القياسي المملح
        var computed = HashPassword(password);
        if (computed.Equals(storedHash, StringComparison.Ordinal)) return true;

        // 3. تطابق الـ Hash بدون ملح (Legacy Hash fallback)
        try
        {
            using var sha256 = SHA256.Create();
            var rawBytes = Encoding.UTF8.GetBytes(password);
            var rawHash = Convert.ToBase64String(sha256.ComputeHash(rawBytes));
            if (rawHash.Equals(storedHash, StringComparison.Ordinal)) return true;
        }
        catch { }

        return false;
    }
}
