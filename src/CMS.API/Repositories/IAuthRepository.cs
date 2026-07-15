using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Data access backing the login flow. Reads credentials and the JWT signing secret.</summary>
public interface IAuthRepository
{
    /// <summary>
    /// Verifies the supplied credentials against <c>AppUser</c>: the <c>UserId</c> must match exactly,
    /// <c>IsActive</c> must be 1, and <c>PasswordHash</c> must equal the SHA-256 of the supplied
    /// password. Returns the authenticated user (with role ids) on success, or <c>null</c> if any check
    /// fails. The password hash never leaves this method.
    /// </summary>
    Task<AuthenticatedUser?> ValidateCredentialsAsync(string userId, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the JWT signing secret at runtime: the <c>symmetricSecurityKey</c> property of the JSON in
    /// <c>SysConfig</c> where <c>configKey = 'appConfig'</c>. Throws <see cref="InvalidOperationException"/>
    /// if the row, JSON, or property is missing.
    /// </summary>
    Task<string> GetSigningSecretAsync(CancellationToken cancellationToken = default);
}
