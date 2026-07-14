using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Data access for <see cref="CourseGroup"/> records.</summary>
public interface ICourseGroupRepository
{
    /// <summary>Returns all course groups ordered by pkid descending.</summary>
    Task<IReadOnlyList<CourseGroup>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns course groups matching the supplied filter.</summary>
    Task<IReadOnlyList<CourseGroup>> QueryAsync(CourseGroupQuery query, CancellationToken cancellationToken = default);

    /// <summary>Returns a single course group or null if not found.</summary>
    Task<CourseGroup?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default);

    /// <summary>Creates a course group; returns the database-assigned pkid.</summary>
    Task<short> CreateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates a course group; returns false if the row does not exist.</summary>
    Task<bool> UpdateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a course group; returns false if the row does not exist.</summary>
    Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken = default);
}
