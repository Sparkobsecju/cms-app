using System.Security.Cryptography;
using System.Text;

namespace CMS.API.Security;

/// <summary>
/// The password-hashing scheme used across the app. New hashes use <b>PBKDF2</b> (HMAC-SHA256, a random
/// per-user salt, and a high iteration count) and are stored self-describing as
/// <c>PBKDF2$SHA256$&lt;iterations&gt;$&lt;saltBase64&gt;$&lt;hashBase64&gt;</c>, so the parameters travel with
/// the hash and can be tuned without a data migration.
/// <para>
/// <see cref="Verify"/> also accepts the <b>deprecated</b> unsalted lower-case-hex SHA-256 hashes written by
/// earlier versions and sets <c>needsRehash</c> so callers can transparently upgrade a row to PBKDF2 on the
/// next successful login (see <c>AuthRepository.ValidateCredentialsAsync</c>). Hashing lives behind this
/// helper so the scheme is defined in exactly one place and can be unit-tested directly.
/// </para>
/// </summary>
public static class PasswordHasher
{
    /// <summary>Marker prefix identifying a PBKDF2 hash string.</summary>
    public const string Pbkdf2Prefix = "PBKDF2";

    private const int Iterations = 100_000;
    private const int SaltBytes = 16;   // 128-bit salt
    private const int HashBytes = 32;   // 256-bit derived key
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    /// <summary>
    /// Returns a PBKDF2 hash of <paramref name="password"/> as
    /// <c>PBKDF2$SHA256$&lt;iterations&gt;$&lt;saltBase64&gt;$&lt;hashBase64&gt;</c>. A fresh random salt is
    /// generated on every call, so the output is non-deterministic by design.
    /// </summary>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var derived = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Iterations, Algorithm, HashBytes);

        return string.Join('$',
            Pbkdf2Prefix,
            "SHA256",
            Iterations.ToString(),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(derived));
    }

    /// <summary>
    /// Verifies <paramref name="password"/> against <paramref name="storedHash"/>. Handles both the current
    /// PBKDF2 format and the deprecated unsalted SHA-256 hex format. When the match succeeds against the
    /// deprecated format, <paramref name="needsRehash"/> is set so the caller can persist a PBKDF2 hash and
    /// retire the weak one. Comparisons are constant-time.
    /// </summary>
    public static bool Verify(string password, string? storedHash, out bool needsRehash)
    {
        needsRehash = false;
        if (string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        if (storedHash.StartsWith(Pbkdf2Prefix + "$", StringComparison.Ordinal))
        {
            return VerifyPbkdf2(password, storedHash);
        }

        // Deprecated scheme: unsalted lower-case hex SHA-256. Accept it for backward compatibility, but
        // signal that the caller should upgrade this hash to PBKDF2 now that we hold the verified plaintext.
        if (VerifyLegacySha256(password, storedHash))
        {
            needsRehash = true;
            return true;
        }

        return false;
    }

    private static bool VerifyPbkdf2(string password, string stored)
    {
        // PBKDF2$SHA256$<iterations>$<saltBase64>$<hashBase64>
        var parts = stored.Split('$');
        if (parts.Length != 5 || !int.TryParse(parts[2], out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expected = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, Algorithm, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static bool VerifyLegacySha256(string password, string storedHex)
    {
        byte[] stored;
        try
        {
            stored = Convert.FromHexString(storedHex);
        }
        catch (FormatException)
        {
            return false; // not a hex hash — nothing we can verify against
        }

        var computed = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return stored.Length == computed.Length
            && CryptographicOperations.FixedTimeEquals(computed, stored);
    }
}
