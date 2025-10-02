using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace BizApp.Utils;

public static class SecurityHash
{
    public static byte[] Sha256(string s)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(s));
    }

    public static string NormalizeEmail(string email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    public static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "";
        var sb = new StringBuilder(phone.Length);
        foreach (var ch in phone)
            if (char.IsDigit(ch)) sb.Append(ch);
        return sb.ToString();
    }

    public static (byte[] hash, byte[] salt) HashPassword(string password, int iterations = 100_000, int bytes = 32)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(bytes);
        return (hash, salt);
    }

    public static bool VerifyPassword(string password, byte[] salt, byte[] expectedHash, int iterations = 100_000, int bytes = 32)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(bytes);
        return CryptographicOperations.FixedTimeEquals(hash, expectedHash);
    }
}
