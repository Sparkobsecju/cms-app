namespace CMS.API.Models;

/// <summary>Search DTO for filtering <see cref="PublishStatus"/> records.</summary>
public class PublishStatusQuery
{
    /// <summary>Keyword; LIKE match on Description.</summary>
    public string? Keyword { get; set; }

    /// <summary>Tri-state filter on IsDraft (null = no filter).</summary>
    public bool? IsDraft { get; set; }

    /// <summary>Tri-state filter on IsPublished (null = no filter).</summary>
    public bool? IsPublished { get; set; }

    /// <summary>Tri-state filter on IsDiscontinued (null = no filter).</summary>
    public bool? IsDiscontinued { get; set; }
}
