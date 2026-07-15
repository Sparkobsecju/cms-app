using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Data access for <see cref="Course"/> records.</summary>
public interface ICourseRepository
{
    /// <summary>Returns all courses (with FK display names) ordered by DisplayOrder ascending.</summary>
    Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns courses matching the supplied filter.</summary>
    Task<IReadOnlyList<Course>> QueryAsync(CourseQuery query, CancellationToken cancellationToken = default);

    /// <summary>Returns a single course (including N-N pkid lists) or null if not found.</summary>
    Task<Course?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default);

    /// <summary>Creates a course; returns the database-assigned pkid.</summary>
    Task<int> CreateAsync(CourseRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates a course; returns false if the row does not exist.</summary>
    Task<bool> UpdateAsync(CourseRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a course; returns false if the row does not exist.</summary>
    Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default);
}
