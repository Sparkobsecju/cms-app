using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>Write DTO for creating/updating an <see cref="AppRole"/>.</summary>
public class AppRoleRequest
{
    /// <summary>Business primary key (角色代碼). Immutable on update.</summary>
    [Required]
    [MaxLength(200)]
    public string RoleId { get; set; } = string.Empty;

    /// <summary>Role display name (角色名稱).</summary>
    [Required]
    [MaxLength(200)]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>Permission level (權限等級). Defaults to 100.</summary>
    [Range(0, int.MaxValue)]
    public int PermissionLevel { get; set; } = 100;

    /// <summary>Optional description (描述).</summary>
    [MaxLength(400)]
    public string? Description { get; set; }

    /// <summary>Assigned user ids (N-N via AppUserRole).</summary>
    public List<string> UserIds { get; set; } = [];
}
