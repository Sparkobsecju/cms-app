using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for creating/updating an <see cref="AppUser"/>.
/// PasswordHash is intentionally absent — it is never accepted from the client
/// (set server-side on create, changed only via the reset-password endpoint).
/// </summary>
public class AppUserRequest
{
    /// <summary>Business primary key (帳號). Immutable on update.</summary>
    [Required]
    [MaxLength(200)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>User display name (使用者名稱).</summary>
    [Required]
    [MaxLength(200)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>Whether the account is active (啟用狀態). Defaults to true.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Assigned role ids (N-N via AppUserRole).</summary>
    public List<string> RoleIds { get; set; } = [];
}
