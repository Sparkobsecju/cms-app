using CMS.API.Models.Lookups;

namespace CMS.API.Repositories;

/// <summary>Data access for slim lookup lists used by form selects.</summary>
public interface ILookupRepository
{
    /// <summary>Returns active application users for the role users multi-select.</summary>
    Task<IReadOnlyList<AppUserLookup>> GetAppUsersAsync(CancellationToken cancellationToken = default);
}
