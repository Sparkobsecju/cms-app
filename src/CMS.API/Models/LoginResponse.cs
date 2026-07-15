namespace CMS.API.Models;

/// <summary>
/// The user profile returned on a successful login. Carries only public identity fields plus the
/// signed access token — never the password hash or any other secret column.
/// </summary>
public sealed class LoginResponse
{
    /// <summary>Business primary key (帳號).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>User display name (使用者名稱).</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>The signed JWT access token (24-hour lifetime).</summary>
    public string AccessToken { get; set; } = string.Empty;
}
