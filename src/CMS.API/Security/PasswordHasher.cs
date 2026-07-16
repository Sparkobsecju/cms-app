using System.Security.Cryptography;
using System.Text;

namespace CMS.API.Security;

/// <summary>
/// The single password-hashing scheme used across the app: SHA-256 of the UTF-8 plaintext, encoded as
/// lower-case hex (64 chars). This is the <b>same</b> scheme <c>AppUserRepository</c> uses when it writes
/// <c>PasswordHash</c>, so a hash produced here compares equal to a stored one. Hashing lives behind this
/// helper so the scheme is defined in exactly one place and can be unit-tested directly.
/// </summary>
public static class PasswordHasher
{
    /// <summary>Returns the lower-case hex SHA-256 hash of <paramref name="password"/>.</summary>
    public static string Hash(string password)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
