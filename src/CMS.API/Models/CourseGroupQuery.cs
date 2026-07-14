namespace CMS.API.Models;

/// <summary>Search DTO for filtering <see cref="CourseGroup"/> records.</summary>
public class CourseGroupQuery
{
    /// <summary>Keyword; LIKE match on Description.</summary>
    public string? Keyword { get; set; }
}
