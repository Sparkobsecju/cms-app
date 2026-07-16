namespace CMS.API.Models;

/// <summary>
/// Request to update the signed-in user's own profile. Carries <b>only</b> the editable
/// <see cref="UserName"/> — the target <c>UserId</c> is always taken from the JWT, never from the
/// body, and roles are not editable here. There is deliberately no <c>UserId</c> property so a
/// client cannot direct the update at another account.
/// </summary>
public sealed class UpdateProfileRequest
{
    /// <summary>New display name (使用者名稱). Required; trimmed and validated server-side.</summary>
    public string UserName { get; set; } = string.Empty;
}
