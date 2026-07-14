namespace CMS.API.Models.Lookups;

/// <summary>Slim lookup row for an application user (used by the role users multi-select).</summary>
public class AppUserLookup
{
    /// <summary>Business primary key (帳號).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Display name (使用者名稱).</summary>
    public string UserName { get; set; } = string.Empty;
}
