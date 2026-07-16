namespace CMS.API.Models;

/// <summary>
/// Request to change the signed-in user's own password. Carries <b>only</b> plaintext passwords, which
/// are hashed and compared server-side and never stored, logged, or returned. There is deliberately no
/// <c>UserId</c> property — the target account is always taken from the JWT, so a client cannot change
/// another user's password. No password hash is ever accepted from the client.
/// </summary>
public sealed class ChangePasswordRequest
{
    /// <summary>The user's current password (目前密碼). Verified against the stored hash.</summary>
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>The desired new password (新密碼). Must satisfy the complexity policy.</summary>
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>Confirmation of the new password (確認新密碼). Must equal <see cref="NewPassword"/>.</summary>
    public string ConfirmPassword { get; set; } = string.Empty;
}
