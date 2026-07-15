namespace CMS.API.Models;

/// <summary>
/// Credentials posted to <c>POST /api/Auth/login</c>. The plaintext password is only ever
/// hashed and compared server-side — it is never stored, logged, or echoed back.
/// </summary>
public sealed class LoginRequest
{
    /// <summary>The account to sign in as (matched exactly against <c>AppUser.UserId</c>).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The supplied plaintext password (SHA-256 hashed before comparison).</summary>
    public string Password { get; set; } = string.Empty;
}
