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

    /// <summary>
    /// Updates the <c>UserName</c> of the given <paramref name="userId"/> (the authenticated user, taken
    /// from the JWT — never from a request body). Touches only <c>UserName</c>; <c>UserId</c>, roles, and
    /// the password hash are left untouched. Returns <c>true</c> when a row was updated, <c>false</c> when
    /// no user with that id exists.
    /// </summary>
    Task<bool> UpdateUserNameAsync(string userId, string userName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies that <paramref name="currentPassword"/> matches the stored <c>PasswordHash</c> for
    /// <paramref name="userId"/>. The comparison is done in SQL (the hash is passed as a parameter, never
    /// selected out), so the stored hash never leaves this method. Returns <c>true</c> on a match.
    /// </summary>
    Task<bool> VerifyCurrentPasswordAsync(string userId, string currentPassword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the given user's <c>PasswordHash</c> to the SHA-256 hash of <paramref name="newPassword"/> and
    /// stamps <c>PasswordUpdatedTime</c> with the current time, in a single update. The plaintext is hashed
    /// inside this method and never stored or returned. Returns <c>true</c> when a row was updated,
    /// <c>false</c> when no user with that id exists. Not row-audited (a self-service action, like the
    /// UserName update and admin reset-password).
    /// </summary>
    Task<bool> ChangePasswordAsync(string userId, string newPassword, CancellationToken cancellationToken = default);
}
