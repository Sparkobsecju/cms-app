namespace CMS.API.Models;

/// <summary>
/// The signed-in user's public profile, returned after a successful self-update. Carries only the
/// identity fields — never the password hash or any secret column.
/// </summary>
public sealed class ProfileResponse
{
    /// <summary>Business primary key (帳號) — read from the JWT, never editable.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The (updated) user display name (使用者名稱).</summary>
    public string UserName { get; set; } = string.Empty;
}
