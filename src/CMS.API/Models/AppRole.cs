namespace CMS.API.Models;

/// <summary>Response model for an application role (角色).</summary>
public class AppRole
{
    /// <summary>Surrogate identity key (主代碼). Display only.</summary>
    public int Pkid { get; set; }

    /// <summary>Business primary key (角色代碼).</summary>
    public string RoleId { get; set; } = string.Empty;

    /// <summary>Role display name (角色名稱).</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>Permission level (權限等級); lower is more privileged.</summary>
    public int PermissionLevel { get; set; }

    /// <summary>Optional description (描述).</summary>
    public string? Description { get; set; }

    /// <summary>Number of users assigned to this role (使用者數).</summary>
    public int UserCount { get; set; }

    /// <summary>Assigned user ids (populated on GET by id via AppUserRole).</summary>
    public List<string> UserIds { get; set; } = [];
}
