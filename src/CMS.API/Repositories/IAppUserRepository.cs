using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Data access for <see cref="AppUser"/> records.</summary>
public interface IAppUserRepository
{
    /// <summary>Returns all users ordered by UserId.</summary>
    Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns users matching the supplied filter.</summary>
    Task<IReadOnlyList<AppUser>> QueryAsync(AppUserQuery query, CancellationToken cancellationToken = default);

    /// <summary>Returns a single user (with assigned role ids) or null if not found.</summary>
    Task<AppUser?> GetByIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Whether a user with the given UserId exists.</summary>
    Task<bool> ExistsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Creates a user (password set from the configured default) and role assignments; returns the new UserId.</summary>
    Task<string> CreateAsync(AppUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates a user and its role assignments (never the password); returns false if the user does not exist.</summary>
    Task<bool> UpdateAsync(AppUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a user and its role assignments; returns false if the user does not exist.</summary>
    Task<bool> DeleteAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Resets the user's password to the configured default; returns false if the user does not exist.</summary>
    Task<bool> ResetPasswordAsync(string userId, CancellationToken cancellationToken = default);
}
