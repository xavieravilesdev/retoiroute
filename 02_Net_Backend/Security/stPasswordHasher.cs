using System.Security.Cryptography;

namespace Commerce.Api.Security;

/// <summary>Hash de contraseñas con PBKDF2-HMAC-SHA256 (600 000 iteraciones, recomendación OWASP).</summary>
public static class stPasswordHasher
{
    private const string Algorithm = "PBKDF2-SHA256";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 600_000;
    private const int MaxIterations = 5_000_000;

    /// <summary>Formato: PBKDF2-SHA256$iteraciones$salt(base64)$hash(base64).</summary>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Algorithm}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != Algorithm) return false;
        if (!int.TryParse(parts[1], out var iterations) || iterations < 1 || iterations > MaxIterations) return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
