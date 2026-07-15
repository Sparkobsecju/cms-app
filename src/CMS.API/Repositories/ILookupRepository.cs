using CMS.API.Models.Lookups;

namespace CMS.API.Repositories;

/// <summary>Data access for slim lookup lists used by form selects.</summary>
public interface ILookupRepository
{
    /// <summary>Returns active application users for the role users multi-select.</summary>
    Task<IReadOnlyList<AppUserLookup>> GetAppUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns publishing statuses for Course/Promotion form selects.</summary>
    Task<IReadOnlyList<PublishStatusLookup>> GetPublishStatusesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns course groups for the Course form select.</summary>
    Task<IReadOnlyList<CourseGroupLookup>> GetCourseGroupsAsync(CancellationToken cancellationToken = default);
}
