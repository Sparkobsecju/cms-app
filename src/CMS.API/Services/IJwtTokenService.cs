namespace CMS.API.Services;

/// <summary>A signed access token together with its UTC expiry.</summary>
public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

/// <summary>Mints signed JWT access tokens for authenticated users.</summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Builds a JWT carrying the user's identity (<c>UserId</c>/<c>UserName</c>) and one role claim per
    /// supplied role, signed (HMAC-SHA256) with <paramref name="signingSecret"/> and expiring 24 hours
    /// after issue.
    /// </summary>
    AccessToken CreateAccessToken(string userId, string userName, IEnumerable<string> roles, string signingSecret);
}
