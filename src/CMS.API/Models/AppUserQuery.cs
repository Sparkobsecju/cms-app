namespace CMS.API.Models;

/// <summary>Search DTO for filtering <see cref="AppUser"/> records.</summary>
public class AppUserQuery
{
    /// <summary>Keyword; LIKE match on UserId and UserName.</summary>
    public string? Keyword { get; set; }

    /// <summary>Tri-state active filter (null = all, true = active, false = inactive).</summary>
    public bool? IsActive { get; set; }
}
