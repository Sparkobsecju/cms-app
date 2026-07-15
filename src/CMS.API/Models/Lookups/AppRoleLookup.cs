namespace CMS.API.Models.Lookups;

/// <summary>Slim lookup row for an application role (used by the AppUser roles multi-select).</summary>
public class AppRoleLookup
{
    /// <summary>Business primary key (角色代碼).</summary>
    public string RoleId { get; set; } = string.Empty;

    /// <summary>Role display name (角色名稱).</summary>
    public string RoleName { get; set; } = string.Empty;
}
