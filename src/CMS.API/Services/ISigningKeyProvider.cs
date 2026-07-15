using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Services;

/// <summary>
/// Supplies the symmetric key used to <b>validate</b> incoming JWTs — the same
/// <c>SysConfig['appConfig'].symmetricSecurityKey</c> the <c>AuthController</c> signs tokens with.
/// </summary>
public interface ISigningKeyProvider
{
    /// <summary>Returns the symmetric signing key (read from config, cached).</summary>
    SecurityKey GetSigningKey();
}
