namespace CMS.API.Models;

/// <summary>Search DTO for filtering <see cref="Partner"/> records.</summary>
public class PartnerQuery
{
    /// <summary>Keyword; LIKE match on Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage.</summary>
    public string? Keyword { get; set; }
}
