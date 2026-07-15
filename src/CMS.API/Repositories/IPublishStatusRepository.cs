using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Data access for <see cref="PublishStatus"/> records.</summary>
public interface IPublishStatusRepository
{
    /// <summary>Returns all statuses ordered by pkid.</summary>
    Task<IReadOnlyList<PublishStatus>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns statuses matching the supplied filter.</summary>
    Task<IReadOnlyList<PublishStatus>> QueryAsync(PublishStatusQuery query, CancellationToken cancellationToken = default);

    /// <summary>Returns a single status or null if not found.</summary>
    Task<PublishStatus?> GetByIdAsync(byte pkid, CancellationToken cancellationToken = default);

    /// <summary>Whether a status with the given pkid exists.</summary>
    Task<bool> ExistsAsync(byte pkid, CancellationToken cancellationToken = default);

    /// <summary>Creates a status; returns the new pkid.</summary>
    Task<byte> CreateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates a status; returns false if the status does not exist.</summary>
    Task<bool> UpdateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a status; returns false if the status does not exist.</summary>
    Task<bool> DeleteAsync(byte pkid, CancellationToken cancellationToken = default);
}
