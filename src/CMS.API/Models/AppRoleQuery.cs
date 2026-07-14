namespace CMS.API.Models;

/// <summary>Search DTO for filtering <see cref="AppRole"/> records.</summary>
public class AppRoleQuery
{
    /// <summary>Keyword; LIKE match on RoleId, RoleName and Description.</summary>
    public string? Keyword { get; set; }

    /// <summary>Exact match on PermissionLevel (權限等級).</summary>
    public int? PermissionLevel { get; set; }
}
