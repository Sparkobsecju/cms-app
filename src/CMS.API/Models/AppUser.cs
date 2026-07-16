namespace CMS.API.Models;

/// <summary>Response model for a system user (使用者). Never exposes the password hash.</summary>
public class AppUser
{
    /// <summary>Surrogate identity key (主代碼). Display only.</summary>
    public int Pkid { get; set; }

    /// <summary>Business primary key (帳號).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>User display name (使用者名稱).</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Whether the account is active (啟用狀態).</summary>
    public bool IsActive { get; set; }

    /// <summary>When the password was last set/reset (密碼更新時間). Display only.</summary>
    public DateTime? PasswordUpdatedTime { get; set; }

    /// <summary>Number of roles assigned to this user (角色數).</summary>
    public int RoleCount { get; set; }

    /// <summary>Assigned role ids (populated on GET by id via AppUserRole).</summary>
    public List<string> RoleIds { get; set; } = [];
}
