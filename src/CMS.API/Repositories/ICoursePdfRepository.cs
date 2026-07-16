using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Read access for building the Course PDF.</summary>
public interface ICoursePdfRepository
{
    /// <summary>
    /// Returns the curated PDF data for a <b>published</b> course by its CourseId,
    /// or null when no published course with that id exists (draft, discontinued,
    /// or unknown all map to null so the endpoint can 404 without confirming existence).
    /// </summary>
    Task<CoursePdf?> GetPublishedForPdfAsync(string courseId, CancellationToken cancellationToken = default);
}
