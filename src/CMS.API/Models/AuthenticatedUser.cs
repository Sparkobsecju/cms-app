namespace CMS.API.Models;

/// <summary>
/// Backend-only result of a successful credential check. Holds just the identity and roles needed
/// to mint a token. Like <see cref="AppUser"/>, it deliberately carries no <c>PasswordHash</c> —
/// the hash never leaves the repository.
/// </summary>
public sealed class AuthenticatedUser
{
    /// <summary>Business primary key (帳號).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>User display name (使用者名稱).</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>The role ids assigned to this user (from <c>AppUserRole</c>); each becomes a role claim.</summary>
    public List<string> RoleIds { get; set; } = [];
}
