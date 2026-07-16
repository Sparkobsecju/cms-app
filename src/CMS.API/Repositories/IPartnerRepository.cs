using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Data access for <see cref="Partner"/> records.</summary>
public interface IPartnerRepository
{
    /// <summary>Returns all partners ordered by display order.</summary>
    Task<IReadOnlyList<Partner>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns partners matching the supplied filter.</summary>
    Task<IReadOnlyList<Partner>> QueryAsync(PartnerQuery query, CancellationToken cancellationToken = default);

    /// <summary>Returns a single partner or null if not found.</summary>
    Task<Partner?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default);

    /// <summary>Creates a partner; returns the database-assigned pkid.</summary>
    Task<short> CreateAsync(PartnerRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates a partner; returns false if the row does not exist.</summary>
    Task<bool> UpdateAsync(PartnerRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a partner; returns false if the row does not exist.</summary>
    Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken = default);
}
