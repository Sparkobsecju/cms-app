using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Services;

/// <summary>
/// Builds signed JWT access tokens. Claims are written verbatim (the handler's outbound claim-type
/// map is cleared per instance) so the token carries exactly the claim names below — no global state
/// is mutated.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    /// <summary>Access tokens are valid for 24 hours from issue.</summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    /// <summary>Claim type carrying the user's <c>UserId</c>.</summary>
    public const string UserIdClaimType = "UserId";

    /// <summary>Claim type carrying the user's <c>UserName</c> (also read by <see cref="RowAuditWriter"/>).</summary>
    public const string UserNameClaimType = "UserName";

    /// <summary>Claim type carrying each assigned role — the .NET default so <c>[Authorize(Roles=…)]</c> works.</summary>
    public const string RoleClaimType = ClaimTypes.Role;

    public AccessToken CreateAccessToken(string userId, string userName, IEnumerable<string> roles, string signingSecret)
    {
        var now = DateTime.UtcNow;
        var expires = now.Add(TokenLifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(UserIdClaimType, userId),
            new(UserNameClaimType, userName),
        };
        claims.AddRange(roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => new Claim(RoleClaimType, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        // Clear the per-instance outbound map so claim types (e.g. the role URI) are emitted as-is.
        var handler = new JwtSecurityTokenHandler();
        handler.OutboundClaimTypeMap.Clear();

        return new AccessToken(handler.WriteToken(token), expires);
    }
}
