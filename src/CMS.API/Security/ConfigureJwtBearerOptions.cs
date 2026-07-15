using CMS.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Security;

/// <summary>
/// Configures JWT bearer validation. The signing key is resolved at validation time from
/// <see cref="ISigningKeyProvider"/> (the same <c>SysConfig['appConfig'].symmetricSecurityKey</c> used to
/// issue tokens), so no secret is baked into startup configuration. Claim types match what
/// <see cref="JwtTokenService"/> emits, so <c>User.Identity.Name</c> and <c>[Authorize(Roles=…)]</c> work.
/// </summary>
public sealed class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly ISigningKeyProvider _keyProvider;

    public ConfigureJwtBearerOptions(ISigningKeyProvider keyProvider)
    {
        _keyProvider = keyProvider;
    }

    public void Configure(string? name, JwtBearerOptions options) => Configure(options);

    public void Configure(JwtBearerOptions options)
    {
        // Claims are emitted verbatim by JwtTokenService, so don't remap them on the way in.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidateIssuerSigningKey = true,
            // Resolve the key on each validation from the provider (read once, then cached).
            IssuerSigningKeyResolver = (_, _, _, _) => new[] { _keyProvider.GetSigningKey() },
            NameClaimType = JwtTokenService.UserNameClaimType,
            RoleClaimType = JwtTokenService.RoleClaimType,
        };
    }
}
