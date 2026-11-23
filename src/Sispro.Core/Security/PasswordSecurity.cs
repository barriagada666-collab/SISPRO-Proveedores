using System.Security.Cryptography;
using System.Text;

namespace Sispro.Core.Security;

/// <summary>
/// Gestión criptográfica de contraseñas con PBKDF2-HMAC-SHA256 y compatibilidad
/// de verificación (solo lectura y migración automática) con el cifrado VB6 heredado.
/// </summary>
public static class PasswordSecurity
{
    private const string Pbkdf2Prefix = "PBKDF2$";
    private const string LegacyPrefix = "LEGACY_VB6$";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int DefaultIterations = 100_000;

    static PasswordSecurity()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static string HashPassword(string plainPassword)
    {
        if (string.IsNullOrEmpty(plainPassword))
        {
            throw new ArgumentException("La contraseña no puede estar vacía.", nameof(plainPassword));
        }

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            plainPassword,
            salt,
            DefaultIterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return $"{Pbkdf2Prefix}{DefaultIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string plainPassword, string storedHash, string usernameAlias, out bool needsUpgrade)
    {
        needsUpgrade = false;
        if (string.IsNullOrEmpty(plainPassword) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        if (storedHash.StartsWith(Pbkdf2Prefix, StringComparison.Ordinal))
        {
            return VerifyPbkdf2(plainPassword, storedHash);
        }

        if (storedHash.StartsWith(LegacyPrefix, StringComparison.Ordinal))
        {
            string expectedHex = storedHash[LegacyPrefix.Length..];
            byte[] computed = ComputeLegacyVb6CipherBytes(plainPassword, usernameAlias);
            byte[] expected;
            try
            {
                expected = Convert.FromHexString(expectedHex);
            }
            catch (FormatException)
            {
                return false;
            }

            bool match = computed.Length == expected.Length &&
                         CryptographicOperations.FixedTimeEquals(computed, expected);
            needsUpgrade = match;
            return match;
        }

        return false;
    }

    private static bool VerifyPbkdf2(string plainPassword, string storedHash)
    {
        string[] parts = storedHash.Split('$');
        if (parts.Length != 4 || !int.TryParse(parts[1], out int iterations) || iterations < 10_000)
        {
            return false;
        }

        try
        {
            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expectedHash = Convert.FromBase64String(parts[3]);
            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                plainPassword,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reproduce exactamente el algoritmo heredado de Utilitarios.bas (encriptar_cadena)
    /// únicamente para permitir la migración transparente de usuarios existentes en OT_PROV.mdb.
    /// </summary>
    public static byte[] ComputeLegacyVb6CipherBytes(string userKey, string text)
    {
        if (string.IsNullOrEmpty(userKey) || string.IsNullOrEmpty(text))
        {
            return [];
        }

        Encoding cp1252 = Encoding.GetEncoding(1252);
        byte[] keyBytes = cp1252.GetBytes(userKey);
        byte[] textBytes = cp1252.GetBytes(text);
        int n = keyBytes.Length;
        byte[] result = new byte[textBytes.Length];

        int j = 0; // En VB6 j es 1-based; aquí lo emulamos idéntico
        for (int i = 0; i < textBytes.Length; i++)
        {
            j = (j + 1 >= n) ? 1 : j + 1;
            int temp = textBytes[i] + keyBytes[j - 1];
            if (temp > 255)
            {
                temp -= 255;
            }

            result[i] = (byte)temp;
        }

        return result;
    }
}
