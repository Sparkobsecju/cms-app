using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Data access for <see cref="AppRole"/> records.</summary>
public interface IAppRoleRepository
{
    /// <summary>Returns all roles ordered by RoleId.</summary>
    Task<IReadOnlyList<AppRole>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns roles matching the supplied filter.</summary>
    Task<IReadOnlyList<AppRole>> QueryAsync(AppRoleQuery query, CancellationToken cancellationToken = default);

    /// <summary>Returns a single role (with assigned user ids) or null if not found.</summary>
    Task<AppRole?> GetByIdAsync(string roleId, CancellationToken cancellationToken = default);

    /// <summary>Whether a role with the given RoleId exists.</summary>
    Task<bool> ExistsAsync(string roleId, CancellationToken cancellationToken = default);

    /// <summary>Creates a role and its user assignments; returns the new RoleId.</summary>
    Task<string> CreateAsync(AppRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates a role and its user assignments; returns false if the role does not exist.</summary>
    Task<bool> UpdateAsync(AppRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a role and its user assignments; returns false if the role does not exist.</summary>
    Task<bool> DeleteAsync(string roleId, CancellationToken cancellationToken = default);
}
